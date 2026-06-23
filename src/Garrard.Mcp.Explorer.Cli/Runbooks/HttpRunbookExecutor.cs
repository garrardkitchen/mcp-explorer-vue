using Garrard.Mcp.Explorer.Core.Domain.HttpApi;
using Garrard.Mcp.Explorer.Core.Interfaces;
using System.Text.Json;

namespace Garrard.Mcp.Explorer.Cli.Runbooks;

public sealed class HttpRunbookExecutor
{
    private readonly IHttpApiStore _store;
    private readonly IHttpApiInvoker _invoker;
    private readonly McpRunbookTemplateResolver _templateResolver;

    public HttpRunbookExecutor(
        IHttpApiStore store,
        IHttpApiInvoker invoker,
        McpRunbookTemplateResolver templateResolver)
    {
        _store = store;
        _invoker = invoker;
        _templateResolver = templateResolver;
    }

    public async Task<IReadOnlyDictionary<string, object?>> ExecuteAsync(
        HttpRunbook runbook,
        Action<string>? progress,
        CancellationToken cancellationToken)
    {
        var runCount = HttpRunbookSchedulePlanner.ComputeRunCount(runbook.Schedule);
        var delay = HttpRunbookSchedulePlanner.ComputeDelay(runbook.Schedule);

        var results = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        for (var runIndex = 0; runIndex < runCount; runIndex++)
        {
            foreach (var step in runbook.Steps)
            {
                var endpoint = await ResolveEndpointAsync(step, cancellationToken).ConfigureAwait(false);
                if (step.UseLocalhost)
                    endpoint = ApplyLocalhostTransform(endpoint);

                var resolvedInputs = (Dictionary<string, object?>)(_templateResolver.ResolveObject(step.Inputs, results, runIndex) ?? new Dictionary<string, object?>());
                var inputs = ToStringInputs(resolvedInputs);

                var stepResult = await ExecuteWithRetriesAsync(
                    endpoint,
                    step,
                    inputs,
                    progress,
                    cancellationToken).ConfigureAwait(false);

                results[step.Id] = stepResult;
            }

            if (delay.HasValue && runIndex < runCount - 1)
                await Task.Delay(delay.Value, cancellationToken).ConfigureAwait(false);
        }

        return results;
    }

    private async Task<object?> ExecuteWithRetriesAsync(
        HttpApiDefinition endpoint,
        HttpRunbookStep step,
        IReadOnlyDictionary<string, string?> inputs,
        Action<string>? progress,
        CancellationToken cancellationToken)
    {
        Exception? last = null;
        var retries = step.MaxRetries;

        for (var attempt = 0; attempt <= retries; attempt++)
        {
            try
            {
                progress?.Invoke($"[{step.Id}] invoking '{endpoint.Name}' (attempt {attempt + 1}/{retries + 1})");
                var result = await _invoker.InvokeAsync(endpoint, inputs, cancellationToken).ConfigureAwait(false);
                return BuildStepResult(result);
            }
            catch (Exception ex)
            {
                last = ex;
                if (attempt == retries)
                {
                    if (step.ContinueOnError)
                    {
                        progress?.Invoke($"[{step.Id}] failed: {ex.Message}. Continuing due to continueOnError=true.");
                        return new Dictionary<string, object?>
                        {
                            ["isSuccess"] = false,
                            ["errorMessage"] = ex.Message,
                            ["stepId"] = step.Id
                        };
                    }

                    throw;
                }
            }
        }

        throw last ?? new InvalidOperationException("Unexpected runbook execution error.");
    }

    private async Task<HttpApiDefinition> ResolveEndpointAsync(HttpRunbookStep step, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(step.EndpointId))
        {
            var byId = await _store.GetDefinitionAsync(step.EndpointId, cancellationToken).ConfigureAwait(false);
            if (byId is not null)
                return byId;

            throw new InvalidOperationException($"Step '{step.Id}' endpointId '{step.EndpointId}' was not found.");
        }

        var all = await _store.GetAllDefinitionsAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(step.Endpoint))
            throw new InvalidOperationException($"Step '{step.Id}' is missing endpoint and endpointId.");

        var exact = all.FirstOrDefault(x => string.Equals(x.Name, step.Endpoint, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
            return exact;

        var matches = all.Where(x => x.Name.Contains(step.Endpoint, StringComparison.OrdinalIgnoreCase)).Take(2).ToList();
        if (matches.Count == 1)
            return matches[0];

        if (matches.Count > 1)
            throw new InvalidOperationException($"Step '{step.Id}' endpoint '{step.Endpoint}' is ambiguous.");

        throw new InvalidOperationException($"Step '{step.Id}' endpoint '{step.Endpoint}' was not found.");
    }

    private static IReadOnlyDictionary<string, string?> ToStringInputs(Dictionary<string, object?> inputs)
    {
        var mapped = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in inputs)
        {
            mapped[key] = value switch
            {
                null => null,
                string s => s,
                JsonElement jsonElement => jsonElement.ValueKind switch
                {
                    JsonValueKind.String => jsonElement.GetString(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    JsonValueKind.Null => null,
                    _ => jsonElement.GetRawText()
                },
                _ => JsonSerializer.Serialize(value)
            };
        }

        return mapped;
    }

    private static object BuildStepResult(HttpApiInvokeResult result)
    {
        object? body = result.Body;
        if (!string.IsNullOrWhiteSpace(result.Body))
        {
            try
            {
                body = JsonSerializer.Deserialize<JsonElement>(result.Body);
            }
            catch
            {
                body = result.Body;
            }
        }

        return new Dictionary<string, object?>
        {
            ["statusCode"] = result.StatusCode,
            ["latencyMs"] = result.LatencyMs,
            ["isSuccess"] = result.IsSuccess,
            ["errorMessage"] = result.ErrorMessage,
            ["body"] = body
        };
    }

    private static HttpApiDefinition ApplyLocalhostTransform(HttpApiDefinition def)
    {
        if (def.BaseUrl.Contains("host.docker.internal", StringComparison.OrdinalIgnoreCase))
        {
            def.BaseUrl = def.BaseUrl.Replace("host.docker.internal", "localhost", StringComparison.OrdinalIgnoreCase);
        }

        return def;
    }
}
