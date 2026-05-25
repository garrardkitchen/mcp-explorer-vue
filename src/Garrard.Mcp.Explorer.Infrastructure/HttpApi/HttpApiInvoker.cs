using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using Azure.Core;
using Azure.Identity;
using Garrard.Mcp.Explorer.Core.Domain.Connections;
using Garrard.Mcp.Explorer.Core.Domain.HttpApi;
using Garrard.Mcp.Explorer.Core.Interfaces;
using Garrard.Mcp.Explorer.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Garrard.Mcp.Explorer.Infrastructure.HttpApi;

/// <summary>
/// Invokes an HTTP endpoint, resolving auth credentials and timing the request.
/// Supports None, CustomHeaders, ApiKey, Bearer, and AzureClientCredentials auth schemes.
/// </summary>
public sealed class HttpApiInvoker : IHttpApiInvoker
{
    // Cap for both display and storage — no separate truncation for history.
    private const int MaxDisplayBodyBytes = 1024 * 1024;
    private static readonly string DefaultUserAgent = $"MCP Explorer/{ResolveVersion()}";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IKeyVaultSecretResolver _keyVaultSecretResolver;
    private readonly ILogger<HttpApiInvoker> _logger;

    public HttpApiInvoker(
        IHttpClientFactory httpClientFactory,
        IKeyVaultSecretResolver keyVaultSecretResolver,
        ILogger<HttpApiInvoker> logger)
    {
        _httpClientFactory = httpClientFactory;
        _keyVaultSecretResolver = keyVaultSecretResolver;
        _logger = logger;
    }

    public Task<HttpApiInvokeResult> InvokeAsync(HttpApiDefinition definition, CancellationToken ct = default)
        => InvokeAsync(definition, inputs: null, ct);

    public async Task<HttpApiInvokeResult> InvokeAsync(
        HttpApiDefinition definition,
        IReadOnlyDictionary<string, string?>? inputs,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var client = _httpClientFactory.CreateClient("HttpApiInvoker");
            var resolvedDefinition = HttpApiTemplateResolver.Apply(definition, inputs);
            using var response = await SendAsync(client, resolvedDefinition, ct)
                .ConfigureAwait(false);

            sw.Stop();

            var body = await ReadBodyAsync(response, ct).ConfigureAwait(false);
            var headers = response.Headers.Concat(response.Content.Headers)
                .ToDictionary(h => h.Key, h => string.Join(", ", h.Value), StringComparer.OrdinalIgnoreCase);

            return new HttpApiInvokeResult
            {
                StatusCode      = (int)response.StatusCode,
                LatencyMs       = sw.ElapsedMilliseconds,
                ResponseHeaders = headers,
                ContentType     = response.Content.Headers.ContentType?.MediaType,
                Body            = body,
                TruncatedBody   = body
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();
            _logger.LogWarning(ex, "HTTP API invocation failed for {Name}", definition.Name);
            return new HttpApiInvokeResult
            {
                StatusCode    = 0,
                LatencyMs     = sw.ElapsedMilliseconds,
                ErrorMessage  = ex.Message
            };
        }
    }

    // ── Request building ──────────────────────────────────────────────────────

    private async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpApiDefinition def, CancellationToken ct)
    {
        var requestUri = BuildUri(def);
        var requestMethod = new HttpMethod(def.Method.ToUpperInvariant());
        var includeBody = requestMethod != HttpMethod.Get && requestMethod != HttpMethod.Head;
        var redirectCount = 0;

        while (true)
        {
            using var request = await BuildRequestAsync(def, requestUri, requestMethod, includeBody, ct).ConfigureAwait(false);
            var response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct).ConfigureAwait(false);

            if (!TryGetRedirectUri(request.Method, requestUri, response, out var redirectUri, out var redirectMethod, out var shouldIncludeBody)
                || redirectCount++ >= 10)
            {
                return response;
            }

            response.Dispose();
            requestUri = redirectUri;
            requestMethod = redirectMethod;
            includeBody = shouldIncludeBody;
        }
    }

    private async Task<HttpRequestMessage> BuildRequestAsync(
        HttpApiDefinition def,
        Uri requestUri,
        HttpMethod method,
        bool includeBody,
        CancellationToken ct)
    {
        var request = new HttpRequestMessage(method, requestUri);

        // Custom headers (always applied first)
        foreach (var h in def.Headers)
            if (!string.IsNullOrWhiteSpace(h.Name))
                request.Headers.TryAddWithoutValidation(h.Name, h.Value);

        // Auth schemes
        switch (def.AuthenticationMode)
        {
            case HttpApiAuthenticationMode.ApiKey when def.ApiKeyOptions is not null:
            {
                var opts = def.ApiKeyOptions;
                var val = string.IsNullOrEmpty(opts.Prefix) ? opts.ApiKey : $"{opts.Prefix}{opts.ApiKey}";
                request.Headers.TryAddWithoutValidation(opts.HeaderName, val);
                break;
            }
            case HttpApiAuthenticationMode.Bearer when def.BearerOptions is not null:
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", def.BearerOptions.Token);
                break;

            case HttpApiAuthenticationMode.AzureClientCredentials when def.AzureCredentials is not null:
            {
                var token = await GetAzureTokenAsync(def.AzureCredentials, ct).ConfigureAwait(false);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                break;
            }
        }

        if (!request.Headers.TryGetValues("User-Agent", out var userAgentValues) ||
            userAgentValues.All(string.IsNullOrWhiteSpace))
        {
            request.Headers.Remove("User-Agent");
            request.Headers.TryAddWithoutValidation("User-Agent", DefaultUserAgent);
        }

        // Body — RFC 7231 allows bodies on all methods except GET and HEAD.
        // DELETE is intentionally allowed here as some APIs use it for batch operations.
        if (includeBody && !string.IsNullOrWhiteSpace(def.BodyTemplate))
        {
            request.Content = new StringContent(def.BodyTemplate, Encoding.UTF8, "application/json");
        }

        return request;
    }

    private static string ResolveVersion()
    {
        var asm = Assembly.GetEntryAssembly();
        var version = asm?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? asm?.GetName().Version?.ToString()
            ?? "0.0.0";

        var plusIdx = version.IndexOf('+');
        if (plusIdx > 0) version = version[..plusIdx];

        return version;
    }

    private static bool TryGetRedirectUri(
        HttpMethod method,
        Uri requestUri,
        HttpResponseMessage response,
        out Uri redirectUri,
        out HttpMethod redirectMethod,
        out bool includeBody)
    {
        redirectUri = requestUri;
        redirectMethod = method;
        includeBody = method != HttpMethod.Get && method != HttpMethod.Head;

        if (response.Headers.Location is not { } location)
        {
            return false;
        }

        if ((int)response.StatusCode is < 300 or > 399)
        {
            return false;
        }

        var nextUri = location.IsAbsoluteUri ? location : new Uri(requestUri, location.OriginalString);
        nextUri = ContainerLocalhostUriRewriter.Rewrite(nextUri);
        if (!string.Equals(nextUri.Scheme, requestUri.Scheme, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!Uri.Compare(nextUri, requestUri, UriComponents.SchemeAndServer, UriFormat.SafeUnescaped, StringComparison.OrdinalIgnoreCase).Equals(0))
        {
            return false;
        }

        if (response.StatusCode == HttpStatusCode.SeeOther
            || ((response.StatusCode == HttpStatusCode.MovedPermanently
                 || response.StatusCode == HttpStatusCode.Found)
                && method != HttpMethod.Get
                && method != HttpMethod.Head))
        {
            redirectMethod = HttpMethod.Get;
            includeBody = false;
        }
        else if (method != HttpMethod.Get
                 && method != HttpMethod.Head
                 && response.StatusCode is not HttpStatusCode.TemporaryRedirect
                 && response.StatusCode is not HttpStatusCode.PermanentRedirect)
        {
            return false;
        }

        redirectUri = nextUri;
        return true;
    }

    private static Uri BuildUri(HttpApiDefinition def)
    {
        var base_ = def.BaseUrl.TrimEnd('/');
        var path  = def.Path.TrimStart('/');
        var url   = string.IsNullOrEmpty(path) ? base_ : $"{base_}/{path}";

        var query = def.QueryParams
            .Where(q => q.Enabled && !string.IsNullOrWhiteSpace(q.Name))
            .Select(q => $"{Uri.EscapeDataString(q.Name)}={Uri.EscapeDataString(q.Value)}");

        var qs = string.Join('&', query);
        var fullUrl = string.IsNullOrEmpty(qs) ? url : $"{url}?{qs}";
        return ContainerLocalhostUriRewriter.Rewrite(new Uri(fullUrl, UriKind.Absolute));
    }

    private async Task<string> GetAzureTokenAsync(HttpApiAzureCredentialsOptions opts, CancellationToken ct)
    {
        TokenCredential credential;
        var clientSecret = await ResolveClientSecretAsync(opts, ct).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(opts.AuthorityHost))
        {
            var options = new ClientSecretCredentialOptions
            {
                AuthorityHost = new Uri(opts.AuthorityHost)
            };
            credential = new ClientSecretCredential(opts.TenantId, opts.ClientId, clientSecret, options);
        }
        else
        {
            credential = new ClientSecretCredential(opts.TenantId, opts.ClientId, clientSecret);
        }

        var ctx   = new TokenRequestContext([opts.Scope]);
        var token = await credential.GetTokenAsync(ctx, ct).ConfigureAwait(false);
        return token.Token;
    }

    private async Task<string> ResolveClientSecretAsync(HttpApiAzureCredentialsOptions opts, CancellationToken ct)
    {
        if (opts.KeyVaultSecretRef is null)
        {
            return opts.ClientSecret;
        }

        return await _keyVaultSecretResolver.ResolveAsync(opts.KeyVaultSecretRef, ct).ConfigureAwait(false);
    }

    private static async Task<string?> ReadBodyAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var bytes = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
        if (bytes.Length == 0) return null;
        var len = Utf8SafeLength(bytes, MaxDisplayBodyBytes);
        return Encoding.UTF8.GetString(bytes, 0, len);
    }

    /// <summary>
    /// Returns the largest byte count ≤ <paramref name="maxBytes"/> that ends on a complete UTF-8 character boundary,
    /// avoiding replacement characters from slicing mid-sequence.
    /// </summary>
    private static int Utf8SafeLength(byte[] bytes, int maxBytes)
    {
        if (bytes.Length <= maxBytes) return bytes.Length;
        var pos = maxBytes;
        // Walk back past continuation bytes (10xxxxxx)
        while (pos > 0 && (bytes[pos - 1] & 0xC0) == 0x80) pos--;
        // If the byte now at pos-1 is a multi-byte start (11xxxxxx), it's incomplete — exclude it too
        if (pos > 0 && (bytes[pos - 1] & 0xC0) == 0xC0) pos--;
        return pos;
    }
}
