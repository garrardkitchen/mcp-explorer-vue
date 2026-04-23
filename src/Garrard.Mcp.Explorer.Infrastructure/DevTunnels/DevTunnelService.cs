using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Garrard.Mcp.Explorer.Core.Domain.DevTunnels;
using Garrard.Mcp.Explorer.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Garrard.Mcp.Explorer.Infrastructure.DevTunnels;

public sealed class DevTunnelService : IDevTunnelService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly Regex NonSlugRegex = new("[^a-z0-9-]", RegexOptions.Compiled);
    private static readonly HashSet<string> SensitiveHeaderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "authorization",
        "cookie",
        "set-cookie",
        "proxy-authorization",
        "x-api-key"
    };

    private readonly IDevTunnelCli _cli;
    private readonly IWebhookEventStore _eventStore;
    private readonly ILogger<DevTunnelService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _baseDirectory;
    private readonly string _statePath;
    private readonly int _capturePort;
    private readonly int _maxCaptureBytes;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _startGates = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _stateGate = new(1, 1);
    private readonly ConcurrentDictionary<string, HostedTunnelRuntime> _hostedTunnels = new(StringComparer.OrdinalIgnoreCase);

    private bool _isLoaded;
    private Dictionary<string, DevTunnel> _tunnels = new(StringComparer.OrdinalIgnoreCase);

    public DevTunnelService(
        IDevTunnelCli cli,
        IWebhookEventStore eventStore,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<DevTunnelService> logger)
    {
        _cli = cli;
        _eventStore = eventStore;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _capturePort = Math.Max(1, configuration.GetValue("DevTunnels:CapturePort", 5000));
        _maxCaptureBytes = Math.Max(1, configuration.GetValue("DevTunnels:MaxCaptureBytes", 1024 * 1024));

        var configuredPath = configuration["DevTunnels:DataPath"]
            ?? Environment.GetEnvironmentVariable("DEVTUNNELS__DataPath");

        _baseDirectory = string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(
                OperatingSystem.IsWindows()
                    ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
                    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share"),
                "McpExplorer",
                "DevTunnels")
            : configuredPath;

        _statePath = Path.Combine(_baseDirectory, "tunnels.json");
    }

    public Task<DevTunnelUserState> GetUserStateAsync(CancellationToken cancellationToken = default)
        => _cli.GetUserStateAsync(cancellationToken);

    public IAsyncEnumerable<string> LoginWithDeviceCodeAsync(CancellationToken cancellationToken = default)
        => _cli.LoginWithDeviceCodeAsync(cancellationToken);

    public async Task<IReadOnlyList<DevTunnel>> ListAsync(CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
        return _tunnels.Values
            .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<DevTunnel?> GetAsync(string tunnelId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tunnelId);
        await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
        return _tunnels.GetValueOrDefault(tunnelId);
    }

    public async Task<DevTunnel> CreateAsync(CreateDevTunnelRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Name);

        await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
        DevTunnel created;

        await _stateGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_tunnels.Values.Any(t => string.Equals(t.Name, request.Name, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"A dev tunnel named '{request.Name}' already exists.");

            var tunnelId = CreateUniqueId(request.Name);
            created = new DevTunnel(
                Id: tunnelId,
                Name: request.Name.Trim(),
                Access: request.Access,
                Status: TunnelStatus.Stopped,
                TunnelUri: null,
                WebhookUri: null,
                CreatedAtUtc: DateTime.UtcNow,
                LastStartedAtUtc: null,
                LastStoppedAtUtc: null,
                LastError: null,
                DeleteOnExit: request.DeleteOnExit,
                RestartCount: 0);

            _tunnels[tunnelId] = created;
            await SaveStateUnlockedAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _stateGate.Release();
        }

        try
        {
            return await StartAsync(created.Id, cancellationToken).ConfigureAwait(false) ?? created;
        }
        catch (DevTunnelCliUnavailableException)
        {
            await _stateGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                _tunnels.Remove(created.Id);
                await SaveStateUnlockedAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _stateGate.Release();
            }

            throw;
        }
    }

    public async Task<DevTunnel?> StartAsync(string tunnelId, CancellationToken cancellationToken = default)
    {
        var startGate = _startGates.GetOrAdd(tunnelId, _ => new SemaphoreSlim(1, 1));
        await startGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var tunnel = await GetAsync(tunnelId, cancellationToken).ConfigureAwait(false);
            if (tunnel is null)
                return null;

            if (_hostedTunnels.ContainsKey(tunnelId))
                return tunnel;

            // The devtunnel host process always needs an authenticated CLI user — the --allow-anonymous
            // flag only relaxes *client* access, not the host's "create" scope on the tunnel domain.
            var user = await _cli.GetUserStateAsync(cancellationToken).ConfigureAwait(false);
            if (!user.IsLoggedIn)
            {
                return await UpdateTunnelAsync(
                    tunnelId,
                    current => current with
                    {
                        Status = TunnelStatus.LoginRequired,
                        LastError = "Dev Tunnel login required before hosting.",
                        LastStoppedAtUtc = DateTime.UtcNow
                    },
                    cancellationToken).ConfigureAwait(false);
            }

            var cliTunnelId = await _cli.EnsureTunnelExistsAsync(
                tunnel.Name,
                tunnel.Access,
                _capturePort,
                existingCliTunnelId: tunnel.CliTunnelId,
                cancellationToken).ConfigureAwait(false);

            // Do NOT link to the request's CancellationToken: the hosted `devtunnel host` process
            // must outlive the HTTP start request. Kestrel can signal RequestAborted after the
            // response completes, which would prematurely tear the tunnel down.
            var cts = new CancellationTokenSource();
            var runtime = new HostedTunnelRuntime(cts);
            if (!_hostedTunnels.TryAdd(tunnelId, runtime))
            {
                runtime.Dispose();
                return await GetAsync(tunnelId, cancellationToken).ConfigureAwait(false);
            }

            try
            {
                var starting = await UpdateTunnelAsync(
                    tunnelId,
                    current => current with
                    {
                        Status = TunnelStatus.Starting,
                        LastStartedAtUtc = DateTime.UtcNow,
                        LastError = null,
                        CliTunnelId = cliTunnelId
                    },
                    cancellationToken).ConfigureAwait(false);

                runtime.Task = MonitorHostAsync(tunnelId, cliTunnelId, tunnel.Access, runtime);
                return starting;
            }
            catch
            {
                _hostedTunnels.TryRemove(tunnelId, out _);
                runtime.Dispose();
                throw;
            }
        }
        finally
        {
            startGate.Release();
        }
    }

    public async Task<DevTunnel?> StopAsync(string tunnelId, CancellationToken cancellationToken = default)
    {
        if (_hostedTunnels.TryRemove(tunnelId, out var runtime))
        {
            runtime.CancellationTokenSource.Cancel();
            try
            {
                if (runtime.Task is not null)
                    await runtime.Task.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                runtime.Dispose();
            }
        }

        return await UpdateTunnelAsync(
            tunnelId,
            current => current with
            {
                Status = TunnelStatus.Stopped,
                LastStoppedAtUtc = DateTime.UtcNow,
                LastError = null
            },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> DeleteAsync(string tunnelId, CancellationToken cancellationToken = default)
    {
        var tunnelForDelete = await GetAsync(tunnelId, cancellationToken).ConfigureAwait(false);
        await StopAsync(tunnelId, cancellationToken).ConfigureAwait(false);

        try
        {
            var cliId = tunnelForDelete?.CliTunnelId ?? tunnelId;
            await _cli.DeleteTunnelAsync(cliId, tunnelForDelete?.Access ?? TunnelAccess.Anonymous, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not delete dev tunnel {TunnelId} from CLI", tunnelId);
        }

        await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
        await _stateGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var removed = _tunnels.Remove(tunnelId);
            if (!removed)
                return false;

            await SaveStateUnlockedAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        finally
        {
            _stateGate.Release();
        }
    }

    public async Task<WebhookEvent> CaptureAsync(WebhookCaptureRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var tunnel = await GetAsync(request.TunnelId, cancellationToken).ConfigureAwait(false);
        if (tunnel is null)
            throw new KeyNotFoundException($"Dev tunnel '{request.TunnelId}' was not found.");

        var headers = RedactHeaders(request.Headers);
        var body = request.Body.Length > _maxCaptureBytes ? request.Body[.._maxCaptureBytes] : request.Body;
        var truncated = request.Body.Length > _maxCaptureBytes;
        var isText = IsTextPayload(request.ContentType);
        var contentEncoding = isText ? "utf-8" : "base64";

        var webhookEvent = new WebhookEvent(
            Id: Guid.NewGuid().ToString("N"),
            TunnelId: request.TunnelId,
            ReceivedAtUtc: DateTime.UtcNow,
            Method: request.Method,
            Path: request.Path,
            QueryString: request.QueryString,
            Headers: headers,
            ContentType: request.ContentType,
            BodySize: request.Body.LongLength,
            BodyText: isText && body.Length > 0 ? Encoding.UTF8.GetString(body) : null,
            BodyBase64: !isText && body.Length > 0 ? Convert.ToBase64String(body) : null,
            ContentEncoding: contentEncoding,
            RemoteIp: request.RemoteIp,
            Truncated: truncated);

        await _eventStore.AppendAsync(webhookEvent, cancellationToken).ConfigureAwait(false);
        return webhookEvent;
    }

    public Task<IReadOnlyList<WebhookEvent>> GetEventsAsync(string tunnelId, int? limit = null, CancellationToken cancellationToken = default)
        => _eventStore.GetRecentAsync(tunnelId, limit, cancellationToken);

    public IAsyncEnumerable<WebhookEvent> StreamEventsAsync(string tunnelId, CancellationToken cancellationToken = default)
        => _eventStore.StreamAsync(tunnelId, cancellationToken);

    public async Task<ReplayWebhookResult> ReplayAsync(ReplayWebhookRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var webhookEvent = await _eventStore.GetAsync(request.TunnelId, request.EventId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Webhook event '{request.EventId}' was not found for tunnel '{request.TunnelId}'.");

        using var message = new HttpRequestMessage(
            new HttpMethod(request.MethodOverride ?? webhookEvent.Method),
            request.TargetUri);

        var outgoingHeaders = request.HeadersOverride ?? webhookEvent.Headers;
        foreach (var header in outgoingHeaders)
        {
            if (header.Key.Equals("host", StringComparison.OrdinalIgnoreCase)
                || header.Key.Equals("content-length", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!message.Headers.TryAddWithoutValidation(header.Key, header.Value))
            {
                message.Content ??= new ByteArrayContent([]);
                message.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        var bodyBytes = ResolveReplayBodyBytes(request, webhookEvent);
        if (bodyBytes.Length > 0)
        {
            message.Content = new ByteArrayContent(bodyBytes);
            if (!string.IsNullOrWhiteSpace(request.ContentTypeOverride ?? webhookEvent.ContentType))
                message.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(request.ContentTypeOverride ?? webhookEvent.ContentType!);
        }

        var startedAt = DateTime.UtcNow;
        using var replaySettings = _httpClientFactory.CreateClient("DevTunnelReplay");
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(replaySettings.Timeout);
        using var handler = CreateReplayHandler(request);
        using var httpClient = new HttpClient(handler, disposeHandler: true);
        using var response = await httpClient.SendAsync(message, timeoutCts.Token).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var duration = DateTime.UtcNow - startedAt;

        var headers = RedactHeaders(response.Headers
            .Concat(response.Content.Headers.Select(h => new KeyValuePair<string, IEnumerable<string>>(h.Key, h.Value)))
            .ToDictionary(h => h.Key, h => string.Join(", ", h.Value), StringComparer.OrdinalIgnoreCase));

        return new ReplayWebhookResult(
            StatusCode: (int)response.StatusCode,
            ReasonPhrase: response.ReasonPhrase,
            Headers: headers,
            BodyText: responseBody,
            BodySize: response.Content.Headers.ContentLength ?? Encoding.UTF8.GetByteCount(responseBody),
            Duration: duration);
    }

    public async Task RestartPersistedRunningTunnelsAsync(CancellationToken cancellationToken)
    {
        var tunnels = await ListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var tunnel in tunnels.Where(t => t.Status is TunnelStatus.Running or TunnelStatus.Starting))
        {
            try
            {
                await StartAsync(tunnel.Id, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not restore tunnel {TunnelId} on startup", tunnel.Id);
            }
        }
    }

    public async Task StopAllAsync(CancellationToken cancellationToken)
    {
        var activeIds = _hostedTunnels.Keys.ToList();
        foreach (var tunnelId in activeIds)
            await StopAsync(tunnelId, cancellationToken).ConfigureAwait(false);
    }

    private async Task MonitorHostAsync(string tunnelId, string cliTunnelId, TunnelAccess access, HostedTunnelRuntime runtime)
    {
        Uri? loggedTunnelUri = null;
        try
        {
            await foreach (var update in _cli.HostAsync(cliTunnelId, access, _capturePort, runtime.CancellationTokenSource.Token).ConfigureAwait(false))
            {
                if (update.TunnelUri is not null && !Uri.Equals(update.TunnelUri, loggedTunnelUri))
                {
                    loggedTunnelUri = update.TunnelUri;
                    var webhookUri = BuildWebhookUri(update.TunnelUri, tunnelId);
                    _logger.LogInformation(
                        "Dev tunnel {TunnelId} is live — tunnel URL: {TunnelUri} · webhook URL: {WebhookUri}",
                        tunnelId,
                        update.TunnelUri,
                        webhookUri);
                }

                await UpdateTunnelAsync(
                    tunnelId,
                    current => current with
                    {
                        // Once a host is Running, subsequent CLI lines (e.g. "Ready to accept
                        // connections…") that don't match any known pattern fall back to
                        // `Starting`. Treat that fallback as "no change" rather than a real
                        // regression so the UI doesn't flicker back to Starting forever.
                        Status = update.Status == TunnelStatus.Starting && current.Status == TunnelStatus.Running
                            ? current.Status
                            : update.Status,
                        TunnelUri = update.TunnelUri ?? current.TunnelUri,
                        WebhookUri = update.TunnelUri is null ? current.WebhookUri : BuildWebhookUri(update.TunnelUri, current.Id),
                        LastError = update.Status is TunnelStatus.Error or TunnelStatus.LoginRequired ? update.OutputLine : null
                    },
                    runtime.CancellationTokenSource.Token).ConfigureAwait(false);
            }

            if (!runtime.CancellationTokenSource.IsCancellationRequested)
            {
                await UpdateTunnelAsync(
                    tunnelId,
                    current => current with
                    {
                        Status = TunnelStatus.Error,
                        LastError = "Dev tunnel host stopped unexpectedly.",
                        LastStoppedAtUtc = DateTime.UtcNow,
                        RestartCount = current.RestartCount + 1
                    },
                    CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (runtime.CancellationTokenSource.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            var looksLikeAuth = LooksLikeAuthFailure(ex.Message);
            if (!looksLikeAuth)
                _logger.LogError(ex, "Dev tunnel host failed for {TunnelId}", tunnelId);
            else
                _logger.LogWarning("Dev tunnel host for {TunnelId} requires re-authentication: {Message}", tunnelId, ex.Message);

            await UpdateTunnelAsync(
                tunnelId,
                current => current with
                {
                    Status = looksLikeAuth ? TunnelStatus.LoginRequired : TunnelStatus.Error,
                    LastError = ex.Message,
                    LastStoppedAtUtc = DateTime.UtcNow,
                    RestartCount = current.RestartCount + 1
                },
                CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            if (_hostedTunnels.TryRemove(tunnelId, out var removedRuntime))
                removedRuntime.Dispose();
        }
    }

    private static bool LooksLikeAuthFailure(string message)
        => !string.IsNullOrWhiteSpace(message) && (
            message.Contains("unauthorized", StringComparison.OrdinalIgnoreCase)
            || message.Contains("not permitted", StringComparison.OrdinalIgnoreCase)
            || message.Contains("access scope", StringComparison.OrdinalIgnoreCase)
            || message.Contains("not logged in", StringComparison.OrdinalIgnoreCase)
            || message.Contains("sign in", StringComparison.OrdinalIgnoreCase)
            || message.Contains("token expired", StringComparison.OrdinalIgnoreCase));

    private async Task EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (_isLoaded)
            return;

        await _stateGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_isLoaded)
                return;

            Directory.CreateDirectory(_baseDirectory);

            if (File.Exists(_statePath))
            {
                var json = await File.ReadAllTextAsync(_statePath, cancellationToken).ConfigureAwait(false);
                var state = JsonSerializer.Deserialize<TunnelState>(json, SerializerOptions);
                _tunnels = state?.Tunnels?.ToDictionary(t => t.Id, StringComparer.OrdinalIgnoreCase)
                    ?? new Dictionary<string, DevTunnel>(StringComparer.OrdinalIgnoreCase);
            }

            _isLoaded = true;
        }
        finally
        {
            _stateGate.Release();
        }
    }

    private async Task<DevTunnel?> UpdateTunnelAsync(string tunnelId, Func<DevTunnel, DevTunnel> update, CancellationToken cancellationToken)
    {
        await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
        await _stateGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_tunnels.TryGetValue(tunnelId, out var existing))
                return null;

            var updated = update(existing);
            _tunnels[tunnelId] = updated;
            await SaveStateUnlockedAsync(cancellationToken).ConfigureAwait(false);
            return updated;
        }
        finally
        {
            _stateGate.Release();
        }
    }

    private async Task SaveStateUnlockedAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_baseDirectory);
        var tempPath = _statePath + ".tmp";
        var payload = JsonSerializer.Serialize(new TunnelState(_tunnels.Values.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase).ToList()), SerializerOptions);

        await File.WriteAllTextAsync(tempPath, payload, cancellationToken).ConfigureAwait(false);
        File.Move(tempPath, _statePath, overwrite: true);
    }

    private string CreateUniqueId(string name)
    {
        var slug = NonSlugRegex.Replace(name.Trim().ToLowerInvariant().Replace(' ', '-'), string.Empty);
        slug = slug.Trim('-');
        if (string.IsNullOrWhiteSpace(slug))
            slug = "tunnel";

        var finalId = slug;
        var version = 2;
        while (_tunnels.ContainsKey(finalId))
        {
            finalId = $"{slug}-{version}";
            version++;
        }

        return finalId;
    }

    private Uri? BuildWebhookUri(Uri? tunnelUri, string tunnelId)
        => tunnelUri is null
            ? null
            : new Uri(tunnelUri, $"/api/v1/webhooks/{Uri.EscapeDataString(tunnelId)}");

    private static bool IsTextPayload(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
            return true;

        return contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
            || contentType.Contains("json", StringComparison.OrdinalIgnoreCase)
            || contentType.Contains("xml", StringComparison.OrdinalIgnoreCase)
            || contentType.Contains("javascript", StringComparison.OrdinalIgnoreCase)
            || contentType.Contains("x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyDictionary<string, string> RedactHeaders(IReadOnlyDictionary<string, string> headers)
        => headers.ToDictionary(
            pair => pair.Key,
            pair => SensitiveHeaderNames.Contains(pair.Key) ? "████" : pair.Value,
            StringComparer.OrdinalIgnoreCase);

    private static byte[] ResolveReplayBodyBytes(ReplayWebhookRequest request, WebhookEvent webhookEvent)
    {
        if (!string.IsNullOrWhiteSpace(request.BodyBase64Override))
            return Convert.FromBase64String(request.BodyBase64Override);

        if (request.BodyTextOverride is not null)
            return Encoding.UTF8.GetBytes(request.BodyTextOverride);

        if (!string.IsNullOrWhiteSpace(webhookEvent.BodyBase64))
            return Convert.FromBase64String(webhookEvent.BodyBase64);

        return webhookEvent.BodyText is null
            ? []
            : Encoding.UTF8.GetBytes(webhookEvent.BodyText);
    }

    private sealed class HostedTunnelRuntime(CancellationTokenSource cancellationTokenSource) : IDisposable
    {
        public CancellationTokenSource CancellationTokenSource { get; } = cancellationTokenSource;
        public Task? Task { get; set; }

        public void Dispose() => CancellationTokenSource.Dispose();
    }

    private sealed record TunnelState(IReadOnlyList<DevTunnel> Tunnels);

    private static SocketsHttpHandler CreateReplayHandler(ReplayWebhookRequest request)
        => new()
        {
            ConnectCallback = async (context, cancellationToken) =>
            {
                Exception? lastError = null;
                foreach (var address in request.AllowedAddresses)
                {
                    var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                    try
                    {
                        await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), cancellationToken).ConfigureAwait(false);
                        return new NetworkStream(socket, ownsSocket: true);
                    }
                    catch (Exception ex)
                    {
                        socket.Dispose();
                        lastError = ex;
                    }
                }

                throw new HttpRequestException(
                    $"Could not connect to an approved replay address for host '{context.DnsEndPoint.Host}'.",
                    lastError);
            }
        };
}
