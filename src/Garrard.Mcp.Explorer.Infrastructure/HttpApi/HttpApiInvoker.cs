using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using Azure.Core;
using Azure.Identity;
using Garrard.Mcp.Explorer.Core.Domain.HttpApi;
using Garrard.Mcp.Explorer.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Garrard.Mcp.Explorer.Infrastructure.HttpApi;

/// <summary>
/// Invokes an HTTP endpoint, resolving auth credentials and timing the request.
/// Supports None, CustomHeaders, ApiKey, Bearer, and AzureClientCredentials auth schemes.
/// </summary>
public sealed class HttpApiInvoker : IHttpApiInvoker
{
    private const int MaxBodyBytes = 4096;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<HttpApiInvoker> _logger;

    public HttpApiInvoker(IHttpClientFactory httpClientFactory, ILogger<HttpApiInvoker> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<HttpApiInvokeResult> InvokeAsync(HttpApiDefinition definition, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var client = _httpClientFactory.CreateClient("HttpApiInvoker");
            using var request = await BuildRequestAsync(definition, ct).ConfigureAwait(false);

            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct)
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
                Body            = body
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

    private async Task<HttpRequestMessage> BuildRequestAsync(HttpApiDefinition def, CancellationToken ct)
    {
        var url = BuildUrl(def);
        var method = new HttpMethod(def.Method.ToUpperInvariant());
        var request = new HttpRequestMessage(method, url);

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

        // Body
        if (!string.IsNullOrWhiteSpace(def.BodyTemplate) &&
            method != HttpMethod.Get && method != HttpMethod.Head && method != HttpMethod.Delete)
        {
            request.Content = new StringContent(def.BodyTemplate, Encoding.UTF8, "application/json");
        }

        return request;
    }

    private static string BuildUrl(HttpApiDefinition def)
    {
        var base_ = def.BaseUrl.TrimEnd('/');
        var path  = def.Path.TrimStart('/');
        var url   = string.IsNullOrEmpty(path) ? base_ : $"{base_}/{path}";

        var query = def.QueryParams
            .Where(q => q.Enabled && !string.IsNullOrWhiteSpace(q.Name))
            .Select(q => $"{Uri.EscapeDataString(q.Name)}={Uri.EscapeDataString(q.Value)}");

        var qs = string.Join('&', query);
        return string.IsNullOrEmpty(qs) ? url : $"{url}?{qs}";
    }

    private static async Task<string> GetAzureTokenAsync(HttpApiAzureCredentialsOptions opts, CancellationToken ct)
    {
        TokenCredential credential;
        if (!string.IsNullOrWhiteSpace(opts.AuthorityHost))
        {
            var options = new ClientSecretCredentialOptions
            {
                AuthorityHost = new Uri(opts.AuthorityHost)
            };
            credential = new ClientSecretCredential(opts.TenantId, opts.ClientId, opts.ClientSecret, options);
        }
        else
        {
            credential = new ClientSecretCredential(opts.TenantId, opts.ClientId, opts.ClientSecret);
        }

        var ctx   = new TokenRequestContext([opts.Scope]);
        var token = await credential.GetTokenAsync(ctx, ct).ConfigureAwait(false);
        return token.Token;
    }

    private static async Task<string?> ReadBodyAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var bytes = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
        if (bytes.Length == 0) return null;
        var truncated = bytes.Length > MaxBodyBytes ? bytes.AsSpan(0, MaxBodyBytes).ToArray() : bytes;
        return Encoding.UTF8.GetString(truncated);
    }
}
