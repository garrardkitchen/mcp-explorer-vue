using System.Text.Json;
using Garrard.Mcp.Explorer.Cli.Commands;
using Garrard.Mcp.Explorer.Core.Domain.Connections;
using Garrard.Mcp.Explorer.Core.Interfaces;

namespace Garrard.Mcp.Explorer.Cli.Runbooks;

public sealed class McpRunbookExecutor
{
    private readonly IConnectionService _connectionService;
    private readonly IUserPreferencesStore _preferencesStore;
    private readonly McpRunbookConnectionFactory _connectionFactory;
    private readonly McpRunbookTemplateResolver _templateResolver;

    public McpRunbookExecutor(
        IConnectionService connectionService,
        IUserPreferencesStore preferencesStore,
        McpRunbookConnectionFactory connectionFactory,
        McpRunbookTemplateResolver templateResolver)
    {
        _connectionService = connectionService;
        _preferencesStore = preferencesStore;
        _connectionFactory = connectionFactory;
        _templateResolver = templateResolver;
    }

    public async Task<RunbookExecutionSummary> ExecuteAsync(
        McpRunbook runbook,
        Action<string>? progress,
        CancellationToken cancellationToken)
    {
        var runCount = McpRunbookSchedulePlanner.ComputeRunCount(runbook.Schedule);
        var delay = McpRunbookSchedulePlanner.ComputeDelay(runbook.Schedule);

        var results = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var connections = await ResolveConnectionsAsync(runbook, cancellationToken).ConfigureAwait(false);
        var failureCount = 0;

        try
        {
            foreach (var definition in connections.Values)
            {
                await _connectionService.ConnectAsync(definition, cancellationToken).ConfigureAwait(false);
            }

            for (var runIndex = 0; runIndex < runCount; runIndex++)
            {
                foreach (var step in runbook.Steps)
                {
                    var resolvedConnectionName = ResolveConnectionName(runbook, step);
                    var parameters = (Dictionary<string, object?>)(_templateResolver.ResolveObject(step.Params, results, runIndex) ?? new Dictionary<string, object?>());

                    var stepResult = await ExecuteWithRetriesAsync(
                        resolvedConnectionName,
                        step,
                        parameters,
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
        }
        finally
        {
            foreach (var definition in connections.Values)
            {
                try
                {
                    await _connectionService.DisconnectAsync(definition.Name, CancellationToken.None).ConfigureAwait(false);
                }
                catch
                {
                    // Best-effort cleanup only
                }
            }
        }

        return new RunbookExecutionSummary(results, failureCount);
    }

    private async Task<Dictionary<string, ConnectionDefinition>> ResolveConnectionsAsync(McpRunbook runbook, CancellationToken cancellationToken)
    {
        var byName = new Dictionary<string, ConnectionDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var connection in runbook.Connections)
        {
            var built = await _connectionFactory.BuildAsync(connection, cancellationToken).ConfigureAwait(false);
            byName[built.Name] = built;
        }

        if (byName.Count == 0)
        {
            var prefs = await _preferencesStore.LoadAsync(cancellationToken).ConfigureAwait(false);
            foreach (var saved in prefs.Connections)
                byName[saved.Name] = saved;
        }

        return byName;
    }

    private static string ResolveConnectionName(McpRunbook runbook, McpRunbookStep step)
    {
        var name = step.Connection ?? runbook.DefaultConnection;
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException($"Step '{step.Id}' is missing a connection and no defaultConnection is set.");

        return name;
    }

    private async Task<object?> ExecuteWithRetriesAsync(
        string connectionName,
        McpRunbookStep step,
        Dictionary<string, object?> parameters,
        Action<string>? progress,
        CancellationToken cancellationToken)
    {
        Exception? last = null;
        var retries = step.MaxRetries;

        for (var attempt = 0; attempt <= retries; attempt++)
        {
            try
            {
                progress?.Invoke($"[{step.Id}] invoking '{step.Tool}' on '{connectionName}' (attempt {attempt + 1}/{retries + 1})");
                var result = await _connectionService.InvokeToolAsync(connectionName, step.Tool, parameters, cancellationToken).ConfigureAwait(false);

                try
                {
                    return JsonSerializer.Deserialize<JsonElement>(result);
                }
                catch
                {
                    return result;
                }
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
                            ["error"] = ex.Message,
                            ["stepId"] = step.Id
                        };
                    }

                    throw;
                }
            }
        }

        throw last ?? new InvalidOperationException("Unexpected runbook execution error.");
    }
}
