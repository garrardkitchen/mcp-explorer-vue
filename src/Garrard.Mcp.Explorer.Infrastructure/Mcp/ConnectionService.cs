using System.Collections.Concurrent;
using Azure.Core;
using Azure.Identity;
using Garrard.Mcp.Explorer.Core.Domain.Connections;
using Garrard.Mcp.Explorer.Core.Interfaces;
using Garrard.Mcp.Explorer.Infrastructure.Elicitation;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol;
using ModelContextProtocol.Authentication;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using System.Text.Json;

namespace Garrard.Mcp.Explorer.Infrastructure.Mcp;

/// <summary>
/// Full MCP connection lifecycle implementation. Manages transport creation,
/// authentication (custom headers, Azure client credentials, OAuth), capability
/// discovery, and optional sampling handler attachment for AI chat integration.
/// </summary>
public sealed class ConnectionService : IConnectionService, IAsyncDisposable
{
    private const string ClientName = "mcp-explorer";
    private const string ClientVersion = "0.5.0";
    private const string DefaultAzureManagementScope = "https://management.azure.com/.default";
    private static readonly TimeSpan OAuthTimeout = TimeSpan.FromMinutes(5);

    private readonly ILogger<ConnectionService> _logger;
    private readonly IConfiguration _configuration;
    private readonly ElicitationService? _elicitationService;
    private readonly OAuthCallbackService _oAuthCallbackService;
    private readonly ConcurrentDictionary<string, ActiveConnection> _connections = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ConnectionDefinition> _definitions = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _reconnectGates = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, AzureTokenCacheEntry> _azureTokenCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _azureTokenSemaphore = new(1, 1);

    public ConnectionService(
        ILogger<ConnectionService> logger,
        IConfiguration configuration,
        IElicitationService elicitationService,
        OAuthCallbackService oAuthCallbackService)
    {
        _logger = logger;
        _configuration = configuration;
        // Cast to concrete type to access HandleElicitationRequestAsync, which is
        // intentionally not on the IElicitationService interface. If the registered
        // implementation is a different type, elicitation handlers are simply skipped.
        _elicitationService = elicitationService as ElicitationService;
        _oAuthCallbackService = oAuthCallbackService;
    }

    public async Task<IActiveConnection> ConnectAsync(
        ConnectionDefinition definition,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (string.IsNullOrWhiteSpace(definition.Endpoint))
        {
            throw new ArgumentException("Endpoint cannot be empty", nameof(definition));
        }

        var endpointUri = RewriteLocalhostForContainer(new Uri(definition.Endpoint, UriKind.Absolute));

        HttpClientTransport? transport = null;
        McpClient? client = null;

        try
        {
            var headers = await BuildConnectionHeadersAsync(definition, cancellationToken).ConfigureAwait(false);

            var transportOptions = new HttpClientTransportOptions
            {
                Endpoint = endpointUri,
                AdditionalHeaders = headers
            };

            if (definition.AuthenticationMode == ConnectionAuthenticationMode.OAuth
                && definition.OAuthOptions is not null)
            {
                transportOptions.OAuth = BuildOAuthOptions(definition.OAuthOptions);
            }

            LogAuthDiagnostics("[MCP Connect]", definition.Name, transportOptions.AdditionalHeaders);

            transport = new HttpClientTransport(transportOptions);

            var clientOptions = BuildClientOptions(definition.Name);
            client = await McpClient.CreateAsync(transport, clientOptions, NullLoggerFactory.Instance, cancellationToken);

            var tools = await client.ListToolsAsync(cancellationToken: cancellationToken);
            var orderedTools = tools.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase).ToArray();

            IReadOnlyList<McpClientPrompt> orderedPrompts = [];
            try
            {
                var prompts = await client.ListPromptsAsync(cancellationToken: cancellationToken);
                orderedPrompts = prompts.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Server does not support prompts");
            }

            IReadOnlyList<McpClientResource> orderedResources = [];
            try
            {
                var resources = await client.ListResourcesAsync(cancellationToken: cancellationToken);
                orderedResources = resources.OrderBy(r => r.Uri, StringComparer.OrdinalIgnoreCase).ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Server does not support resources");
            }

            IReadOnlyList<McpClientResourceTemplate> orderedResourceTemplates = [];
            try
            {
                var templates = await client.ListResourceTemplatesAsync(cancellationToken: cancellationToken);
                orderedResourceTemplates = templates.OrderBy(t => t.UriTemplate, StringComparer.OrdinalIgnoreCase).ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Server does not support resource templates");
            }

            if (_connections.TryRemove(definition.Name, out var existing))
            {
                await existing.DisposeAsync().ConfigureAwait(false);
            }

            var context = new ConnectionContext(
                definition.Name,
                endpointUri.ToString(),
                ClientName,
                ClientVersion,
                definition.AuthenticationMode,
                definition.AzureCredentials,
                definition.OAuthOptions,
                transportOptions.AdditionalHeaders is null
                    ? new Dictionary<string, string>()
                    : new Dictionary<string, string>(transportOptions.AdditionalHeaders),
                transport,
                client,
                orderedTools,
                orderedPrompts,
                orderedResources,
                orderedResourceTemplates);

            var activeConnection = new ActiveConnection(context);

            // Swap atomically: store the new connection first, then dispose the old one.
            // This ensures GetConnection() never returns null during the transition.
            var previousConnection = _connections.GetValueOrDefault(definition.Name);
            _connections[definition.Name] = activeConnection;
            if (previousConnection is not null)
            {
                await previousConnection.DisposeAsync().ConfigureAwait(false);
            }

            _logger.LogInformation("Connected to {ConnectionName}", definition.Name);
            _definitions[definition.Name] = definition;
            return activeConnection;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to {ConnectionName}", definition.Name);

            if (client is not null) await client.DisposeAsync().ConfigureAwait(false);
            if (transport is not null) await transport.DisposeAsync().ConfigureAwait(false);

            throw;
        }
    }

    public IReadOnlyList<IActiveConnection> GetActiveConnections()
        => _connections.Values.Cast<IActiveConnection>().ToList();

    public IActiveConnection? GetConnection(string name)
        => _connections.TryGetValue(name, out var conn) ? conn : null;

    public async Task DisconnectAsync(string name, CancellationToken cancellationToken = default)
    {
        _definitions.TryRemove(name, out _);
        if (_connections.TryRemove(name, out var connection))
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            _logger.LogInformation("Disconnected from {ConnectionName}", name);
        }
    }

    public async Task DisconnectAllAsync(CancellationToken cancellationToken = default)
    {
        var connections = _connections.Values.ToList();
        _connections.Clear();

        foreach (var connection in connections)
        {
            try
            {
                await connection.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing connection {ConnectionName}", connection.Name);
            }
        }
    }

    public async Task<string> InvokeToolAsync(
        string connectionName,
        string toolName,
        Dictionary<string, object?> parameters,
        CancellationToken cancellationToken = default)
    {
        var maxAttempts = Math.Max(1, _configuration.GetValue<int>("ToolInvoke:MaxRetryAttempts", 2));

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var connection = GetActiveConnectionOrThrow(connectionName);
                var result = await connection.Context.Client
                    .CallToolAsync(toolName, parameters, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                // Successful call — restore health in case a previous attempt had degraded it.
                connection.IsHealthy = true;
                return McpToolResultHelper.ConvertToJson(result);
            }
            catch (Exception ex) when (IsTransientConnectionError(ex, cancellationToken))
            {
                if (attempt < maxAttempts)
                {
                    _logger.LogWarning(ex,
                        "Transient error invoking tool '{ToolName}' on '{ConnectionName}' " +
                        "(attempt {Attempt}/{Max}), reconnecting…",
                        toolName, connectionName, attempt, maxAttempts);

                    try
                    {
                        await ReconnectAsync(connectionName, cancellationToken).ConfigureAwait(false);
                        // Loop continues to next attempt naturally.
                    }
                    catch (Exception reconnectEx)
                    {
                        // Reconnect itself failed — mark unhealthy and surface the reconnect error.
                        if (_connections.TryGetValue(connectionName, out var d))
                        {
                            d.IsHealthy = false;
                            _logger.LogError(reconnectEx,
                                "Reconnect failed for '{ConnectionName}', marking as unhealthy.", connectionName);
                        }
                        throw;
                    }
                }
                else
                {
                    // All attempts exhausted — mark the connection as degraded so the UI
                    // can display a reconnect indicator, then re-throw to the caller.
                    if (_connections.TryGetValue(connectionName, out var degraded))
                    {
                        degraded.IsHealthy = false;
                        _logger.LogError(ex,
                            "All {Max} retry attempts for tool '{ToolName}' on '{ConnectionName}' exhausted. " +
                            "Connection marked as unhealthy.",
                            maxAttempts, toolName, connectionName);
                    }
                    throw;
                }
            }
        }

        // Unreachable — the last attempt either returns or throws without being caught above.
        throw new InvalidOperationException("Exhausted retry attempts.");
    }

    /// <summary>
    /// Returns true for transport-level failures that are worth retrying after a
    /// reconnect. Server-side errors (McpException), user cancellation, and
    /// argument errors are NOT retried.
    /// </summary>
    private static bool IsTransientConnectionError(Exception ex, CancellationToken ct)
    {
        // Never retry when the caller's token was explicitly cancelled (timeout / user cancel).
        if (ct.IsCancellationRequested) return false;

        return ex is HttpRequestException
            or ObjectDisposedException
            or System.IO.IOException
            or TaskCanceledException { InnerException: HttpRequestException };
        // McpException (server returned an error) is intentionally NOT retried.
    }

    private async Task ReconnectAsync(string connectionName, CancellationToken cancellationToken)
    {
        if (!_definitions.TryGetValue(connectionName, out var definition))
        {
            throw new InvalidOperationException(
                $"Cannot reconnect '{connectionName}': connection definition not found. " +
                "The connection may have been manually disconnected.");
        }

        // Serialise concurrent reconnect attempts for the same connection.
        // If another caller is already reconnecting, wait for it to finish and
        // skip the redundant reconnect if the connection is healthy again.
        var gate = _reconnectGates.GetOrAdd(connectionName, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Another concurrent caller may have already reconnected successfully.
            if (_connections.TryGetValue(connectionName, out var existing) && existing.IsHealthy)
            {
                _logger.LogDebug(
                    "Reconnect for '{ConnectionName}' skipped — already recovered by a concurrent caller.",
                    connectionName);
                return;
            }

            _logger.LogInformation("Reconnecting to '{ConnectionName}'…", connectionName);
            await ConnectAsync(definition, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<string> ExecutePromptAsync(
        string connectionName,
        string promptName,
        Dictionary<string, string> arguments,
        CancellationToken cancellationToken = default)
    {
        var connection = GetActiveConnectionOrThrow(connectionName);
        var sdkArgs = arguments.ToDictionary(
            kvp => kvp.Key,
            kvp => (object?)kvp.Value,
            StringComparer.OrdinalIgnoreCase);
        var result = await connection.Context.Client
            .GetPromptAsync(promptName, sdkArgs, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return JsonSerializer.Serialize(result);
    }

    public async Task<string> ReadResourceAsync(
        string connectionName,
        string uri,
        CancellationToken cancellationToken = default)
    {
        var connection = GetActiveConnectionOrThrow(connectionName);
        var result = await connection.Context.Client
            .ReadResourceAsync(uri, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return JsonSerializer.Serialize(result.Contents);
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAllAsync().ConfigureAwait(false);
        _azureTokenSemaphore.Dispose();
    }

    /// <summary>
    /// Returns the raw <see cref="ConnectionContext"/> for a named connection.
    /// Intended for use by <c>AiChatService</c> when wiring sampling handlers.
    /// </summary>
    internal ConnectionContext? GetConnectionContext(string name)
        => _connections.TryGetValue(name, out var conn) ? conn.Context : null;

    /// <summary>
    /// Attaches a sampling handler backed by <paramref name="chatClient"/> to all active
    /// connections that do not yet have one. Each affected connection is rebuilt with a new
    /// MCP client so that the sampling capability is advertised server-side.
    /// Safe to call multiple times — already-attached connections are skipped.
    /// </summary>
    internal async Task AttachSamplingHandlerAsync(
        IChatClient chatClient,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chatClient);

        var samplingHandler = AIContentExtensions.CreateSamplingHandler(chatClient);
        var snapshot = _connections.Values.ToList();

        foreach (var existing in snapshot)
        {
            if (existing.Context.HasSamplingHandler) continue;

            try
            {
                var name = existing.Context.Name;
                var endpoint = new Uri(existing.Context.Endpoint, UriKind.Absolute);

                var rebuiltHeaders = await BuildConnectionHeadersAsync(existing.Context, cancellationToken).ConfigureAwait(false);

                var transportOptions = new HttpClientTransportOptions
                {
                    Endpoint = endpoint,
                    AdditionalHeaders = rebuiltHeaders
                };

                if (existing.Context.AuthenticationMode == ConnectionAuthenticationMode.OAuth
                    && existing.Context.OAuthOptions is not null)
                {
                    transportOptions.OAuth = BuildOAuthOptions(existing.Context.OAuthOptions);
                }

                LogAuthDiagnostics("[MCP Sampling]", name, transportOptions.AdditionalHeaders);

                HttpClientTransport? newTransport = null;
                McpClient? newClient = null;
                try
                {
                newTransport = new HttpClientTransport(transportOptions);
                var clientOptions = BuildClientOptions(name, samplingHandler);
                newClient = await McpClient.CreateAsync(newTransport, clientOptions, NullLoggerFactory.Instance, cancellationToken).ConfigureAwait(false);

                var tools = await newClient.ListToolsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
                var orderedTools = tools.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase).ToArray();

                IReadOnlyList<McpClientPrompt> orderedPrompts = [];
                try
                {
                    var prompts = await newClient.ListPromptsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
                    orderedPrompts = prompts.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToArray();
                }
                catch (Exception ex) { _logger.LogDebug(ex, "Server does not support prompts"); }

                IReadOnlyList<McpClientResource> orderedResources = [];
                try
                {
                    var resources = await newClient.ListResourcesAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
                    orderedResources = resources.OrderBy(r => r.Uri, StringComparer.OrdinalIgnoreCase).ToArray();
                }
                catch (Exception ex) { _logger.LogDebug(ex, "Server does not support resources"); }

                IReadOnlyList<McpClientResourceTemplate> orderedResourceTemplates = [];
                try
                {
                    var templates = await newClient.ListResourceTemplatesAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
                    orderedResourceTemplates = templates.OrderBy(t => t.UriTemplate, StringComparer.OrdinalIgnoreCase).ToArray();
                }
                catch (Exception ex) { _logger.LogDebug(ex, "Server does not support resource templates"); }

                var replacement = new ConnectionContext(
                    existing.Context.Name,
                    existing.Context.Endpoint,
                    existing.Context.ClientName,
                    existing.Context.ClientVersion,
                    existing.Context.AuthenticationMode,
                    existing.Context.AzureCredentials,
                    existing.Context.OAuthOptions,
                    transportOptions.AdditionalHeaders is null
                        ? new Dictionary<string, string>()
                        : new Dictionary<string, string>(transportOptions.AdditionalHeaders),
                    newTransport,
                    newClient,
                    orderedTools,
                    orderedPrompts,
                    orderedResources,
                    orderedResourceTemplates)
                {
                    HasSamplingHandler = true
                };

                // Transfer ownership: newTransport and newClient are now owned by replacement.
                newTransport = null;
                newClient = null;

                var newActiveConnection = new ActiveConnection(replacement);

                if (_connections.TryUpdate(name, newActiveConnection, existing))
                {
                    await existing.DisposeAsync().ConfigureAwait(false);
                }
                else
                {
                    await newActiveConnection.DisposeAsync().ConfigureAwait(false);
                    _logger.LogDebug("Sampling handler attach lost race for {ConnectionName}; disposed replacement", name);
                }

                _logger.LogInformation("Attached sampling handler for connection {ConnectionName}", name);
                }
                catch (Exception)
                {
                    if (newClient is not null) await newClient.DisposeAsync().ConfigureAwait(false);
                    if (newTransport is not null) await newTransport.DisposeAsync().ConfigureAwait(false);
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to attach sampling handler for {ConnectionName}", existing.Name);
            }
        }
    }

    private ActiveConnection GetActiveConnectionOrThrow(string name)
    {
        if (!_connections.TryGetValue(name, out var connection))
        {
            throw new InvalidOperationException($"No active connection named '{name}'.");
        }

        return connection;
    }

    private McpClientOptions BuildClientOptions(
        string connectionName,
        Func<CreateMessageRequestParams?, IProgress<ProgressNotificationValue>, CancellationToken, ValueTask<CreateMessageResult>>? samplingHandler = null)
    {
        return new McpClientOptions
        {
            ClientInfo = new Implementation
            {
                Name = ClientName,
                Title = "MCP Explorer",
                Version = ClientVersion
            },
            Capabilities = new ClientCapabilities
            {
                Sampling = new SamplingCapability(),
                Elicitation = new ElicitationCapability()
            },
            Handlers = new McpClientHandlers
            {
                SamplingHandler = samplingHandler,
                ElicitationHandler = _elicitationService is null
                    ? null
                    : (requestParams, token) =>
                        _elicitationService.HandleElicitationRequestAsync(connectionName, requestParams, token)
            }
        };
    }

    private async Task<Dictionary<string, string>> BuildConnectionHeadersAsync(
        ConnectionDefinition definition,
        CancellationToken cancellationToken)
    {
        var headers = MaterializeHeaders(definition.Headers);

        if (definition.AuthenticationMode == ConnectionAuthenticationMode.AzureClientCredentials)
        {
            if (definition.AzureCredentials is null)
            {
                throw new InvalidOperationException($"Connection '{definition.Name}' is missing Azure credentials.");
            }

            var token = await AcquireAzureAccessTokenAsync(definition.AzureCredentials, cancellationToken).ConfigureAwait(false);
            headers["Authorization"] = $"Bearer {token}";
        }

        return headers;
    }

    private async Task<Dictionary<string, string>> BuildConnectionHeadersAsync(
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var headers = new Dictionary<string, string>(context.Headers, StringComparer.OrdinalIgnoreCase);

        if (context.AuthenticationMode == ConnectionAuthenticationMode.AzureClientCredentials)
        {
            if (context.AzureCredentials is null)
            {
                throw new InvalidOperationException($"Connection '{context.Name}' is missing Azure credentials.");
            }

            var token = await AcquireAzureAccessTokenAsync(context.AzureCredentials, cancellationToken).ConfigureAwait(false);
            headers["Authorization"] = $"Bearer {token}";
        }

        return headers;
    }

    private async Task<string> AcquireAzureAccessTokenAsync(
        AzureClientCredentialsOptions credentials,
        CancellationToken cancellationToken)
    {
        var tenantId = credentials.TenantId.Trim();
        var clientId = credentials.ClientId.Trim();
        var authorityHost = string.IsNullOrWhiteSpace(credentials.AuthorityHost)
            ? "https://login.microsoftonline.com"
            : credentials.AuthorityHost.Trim();

        var scopes = credentials.Scope
            .Split([' ', '\t', '\r', '\n', ';', ','], StringSplitOptions.RemoveEmptyEntries)
            .Select(static s => s.Trim())
            .Where(static s => !string.IsNullOrEmpty(s))
            .ToArray();

        if (scopes.Length == 0)
        {
            scopes = [DefaultAzureManagementScope];
        }

        var cacheKey = string.Join("|", tenantId, clientId, authorityHost, string.Join(' ', scopes));

        if (_azureTokenCache.TryGetValue(cacheKey, out var cached)
            && cached.Token.ExpiresOn > DateTimeOffset.UtcNow.AddMinutes(2))
        {
            return cached.Token.Token;
        }

        await _azureTokenSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_azureTokenCache.TryGetValue(cacheKey, out cached)
                && cached.Token.ExpiresOn > DateTimeOffset.UtcNow.AddMinutes(2))
            {
                return cached.Token.Token;
            }

            var credentialOptions = new ClientSecretCredentialOptions();
            if (!string.IsNullOrWhiteSpace(credentials.AuthorityHost))
            {
                if (!Uri.TryCreate(credentials.AuthorityHost, UriKind.Absolute, out var authorityUri))
                {
                    throw new InvalidOperationException($"Invalid authority host '{credentials.AuthorityHost}'.");
                }

                credentialOptions.AuthorityHost = authorityUri;
            }

            var credential = new ClientSecretCredential(tenantId, clientId, credentials.ClientSecret, credentialOptions);
            var tokenContext = new TokenRequestContext(scopes);
            var token = await credential.GetTokenAsync(tokenContext, cancellationToken).ConfigureAwait(false);

            _azureTokenCache[cacheKey] = new AzureTokenCacheEntry(token);
            return token.Token;
        }
        finally
        {
            _azureTokenSemaphore.Release();
        }
    }

    private static Dictionary<string, string> MaterializeHeaders(IEnumerable<ConnectionHeader> headersSource)
        => ConnectionHeaderBuilder.Build(headersSource);

    /// <summary>
    /// Builds <see cref="ClientOAuthOptions"/> from the stored OAuth connection options,
    /// wiring the <see cref="OAuthCallbackService"/> as the authorization redirect handler.
    /// </summary>
    private ClientOAuthOptions BuildOAuthOptions(OAuthConnectionOptions opts)
    {
        var scopes = opts.Scopes
            .Split([' ', ',', ';', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(static s => s.Trim())
            .Where(static s => !string.IsNullOrEmpty(s))
            .ToArray();

        return new ClientOAuthOptions
        {
            ClientId = opts.ClientId,
            ClientSecret = string.IsNullOrWhiteSpace(opts.ClientSecret) ? null : opts.ClientSecret,
            Scopes = scopes.Length > 0 ? scopes : null,
            RedirectUri = string.IsNullOrWhiteSpace(opts.RedirectUri)
                ? null!
                : new Uri(opts.RedirectUri, UriKind.Absolute),
            ClientMetadataDocumentUri = string.IsNullOrWhiteSpace(opts.ClientMetadataDocumentUri)
                ? null
                : new Uri(opts.ClientMetadataDocumentUri, UriKind.Absolute),
            AuthorizationRedirectDelegate = async (authUri, redirectUri, ct) =>
            {
                var callbackUri = await _oAuthCallbackService
                    .AwaitCallbackAsync(authUri, OAuthTimeout, ct)
                    .ConfigureAwait(false);
                return callbackUri?.ToString();
            }
        };
    }

    /// <summary>
    /// When running inside a Docker container, rewrites <c>localhost</c> / <c>127.0.0.1</c>
    /// endpoints to <c>host.docker.internal</c> so they resolve to the host machine.
    /// Detected via the <c>DOTNET_RUNNING_IN_CONTAINER</c> env var set by all official
    /// Microsoft .NET base images. No-op when not in a container or when the host is
    /// already <c>host.docker.internal</c>.
    /// </summary>
    private static Uri RewriteLocalhostForContainer(Uri uri)
    {
        var isContainer = string.Equals(
            Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"),
            "true",
            StringComparison.OrdinalIgnoreCase);

        if (!isContainer) return uri;

        var host = uri.Host;
        if (!string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
            && host != "127.0.0.1"
            && host != "::1")
        {
            return uri;
        }

        var builder = new UriBuilder(uri) { Host = "host.docker.internal" };
        return builder.Uri;
    }

    private void LogAuthDiagnostics(string prefix, string connectionName, IDictionary<string, string>? headers)
    {
        string? authValue = null;
        var hasAuth = headers?.TryGetValue("Authorization", out authValue) ?? false;
        string? scheme = null;
        if (hasAuth && !string.IsNullOrWhiteSpace(authValue))
        {
            var idx = authValue.IndexOf(' ');
            scheme = idx > 0 ? authValue[..idx] : "(present)";
        }

        _logger.LogInformation(
            "{Prefix} Headers for {ConnectionName}: Authorization present={HasAuth}, scheme={Scheme}, keys=[{Keys}]",
            prefix, connectionName, hasAuth, scheme ?? "(none)",
            string.Join(", ", headers?.Keys ?? []));
    }

    private sealed record AzureTokenCacheEntry(AccessToken Token);
}
