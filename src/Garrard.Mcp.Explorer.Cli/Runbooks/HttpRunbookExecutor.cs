using Garrard.Mcp.Explorer.Core.Domain.HttpApi;
using Garrard.Mcp.Explorer.Core.Domain.Connections;
using Garrard.Mcp.Explorer.Core.Interfaces;
using System.Text.Json;

namespace Garrard.Mcp.Explorer.Cli.Runbooks;

public sealed class HttpRunbookExecutor
{
    private readonly IHttpApiStore _store;
    private readonly IHttpApiInvoker _invoker;
    private readonly HttpRunbookConnectionFactory _connectionFactory;
    private readonly RunbookTemplateResolver _templateResolver;

    public HttpRunbookExecutor(
        IHttpApiStore store,
        IHttpApiInvoker invoker,
        HttpRunbookConnectionFactory connectionFactory,
        RunbookTemplateResolver templateResolver)
    {
        _store = store;
        _invoker = invoker;
        _connectionFactory = connectionFactory;
        _templateResolver = templateResolver;
    }

    public async Task<RunbookExecutionSummary> ExecuteAsync(
        HttpRunbook runbook,
        Action<string>? progress,
        CancellationToken cancellationToken)
    {
        var runCount = RunbookSchedulePlanner.ComputeRunCount(runbook.Schedule);
        var delay = RunbookSchedulePlanner.ComputeDelay(runbook.Schedule);

        var results = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var inlineConnections = await ResolveConnectionsAsync(runbook, cancellationToken).ConfigureAwait(false);
        var failureCount = 0;

        for (var runIndex = 0; runIndex < runCount; runIndex++)
        {
            foreach (var step in runbook.Steps)
            {
                var endpoint = await ResolveEndpointAsync(runbook, step, inlineConnections, cancellationToken).ConfigureAwait(false);
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

                if (step.Assert is not null)
                {
                    var assertion = RunbookAssertionEvaluator.Evaluate(stepResult, step.Assert);
                    if (!assertion.IsSuccess)
                    {
                        failureCount++;
                        var message = $"[{step.Id}] {assertion.Message}";
                        progress?.Invoke(message);

                        var continueOnAssertFailure = step.ContinueOnAssertFailure ?? runbook.ContinueOnAssertFailure;
                        if (!continueOnAssertFailure)
                            throw new InvalidOperationException(message);
                    }
                }
            }

            if (delay.HasValue && runIndex < runCount - 1)
                await Task.Delay(delay.Value, cancellationToken).ConfigureAwait(false);
        }

        return new RunbookExecutionSummary(results, failureCount);
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

    private async Task<Dictionary<string, HttpApiDefinition>> ResolveConnectionsAsync(HttpRunbook runbook, CancellationToken cancellationToken)
    {
        var byName = new Dictionary<string, HttpApiDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var connection in runbook.Connections)
        {
            var built = await _connectionFactory.BuildAsync(connection, cancellationToken).ConfigureAwait(false);
            byName[built.Name] = built;
        }

        return byName;
    }

    private async Task<HttpApiDefinition> ResolveEndpointAsync(
        HttpRunbook runbook,
        HttpRunbookStep step,
        IReadOnlyDictionary<string, HttpApiDefinition> inlineConnections,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(step.EndpointId))
        {
            var byId = await _store.GetDefinitionAsync(step.EndpointId, cancellationToken).ConfigureAwait(false);
            if (byId is not null)
                return byId;

            throw new InvalidOperationException($"Step '{step.Id}' endpointId '{step.EndpointId}' was not found.");
        }

        var endpointName = string.IsNullOrWhiteSpace(step.Endpoint)
            ? runbook.DefaultConnection
            : step.Endpoint;
        if (string.IsNullOrWhiteSpace(endpointName))
            throw new InvalidOperationException($"Step '{step.Id}' is missing endpoint and endpointId (and no defaultConnection is set).");

        if (inlineConnections.TryGetValue(endpointName, out var inline))
            return inline;

        var inlineMatches = inlineConnections
            .Where(x => x.Key.Contains(endpointName, StringComparison.OrdinalIgnoreCase))
            .Take(2)
            .Select(x => x.Value)
            .ToList();
        if (inlineMatches.Count == 1)
            return inlineMatches[0];

        if (inlineMatches.Count > 1)
            throw new InvalidOperationException($"Step '{step.Id}' endpoint '{endpointName}' is ambiguous.");

        var all = await _store.GetAllDefinitionsAsync(cancellationToken).ConfigureAwait(false);
        var exact = all.FirstOrDefault(x => string.Equals(x.Name, endpointName, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
            return exact;

        var matches = all.Where(x => x.Name.Contains(endpointName, StringComparison.OrdinalIgnoreCase)).Take(2).ToList();
        if (matches.Count == 1)
            return matches[0];

        if (matches.Count > 1)
            throw new InvalidOperationException($"Step '{step.Id}' endpoint '{endpointName}' is ambiguous.");

        throw new InvalidOperationException($"Step '{step.Id}' endpoint '{endpointName}' was not found.");
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
        if (!def.BaseUrl.Contains("host.docker.internal", StringComparison.OrdinalIgnoreCase))
            return def;

        var clone = CloneDefinition(def);
        clone.BaseUrl = clone.BaseUrl.Replace("host.docker.internal", "localhost", StringComparison.OrdinalIgnoreCase);
        return clone;
    }

    // JSON round-trip so new HttpApiDefinition properties are never silently dropped from the copy.
    private static HttpApiDefinition CloneDefinition(HttpApiDefinition definition)
        => JsonSerializer.Deserialize<HttpApiDefinition>(JsonSerializer.SerializeToUtf8Bytes(definition))
           ?? throw new InvalidOperationException("Failed to clone endpoint definition.");
}
