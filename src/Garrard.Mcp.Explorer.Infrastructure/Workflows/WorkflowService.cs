using System.Text.Json;
using System.Text.Json.Nodes;
using Garrard.Mcp.Explorer.Core.Domain.Connections;
using Garrard.Mcp.Explorer.Core.Domain.Preferences;
using Garrard.Mcp.Explorer.Core.Domain.Workflows;
using Garrard.Mcp.Explorer.Core.Interfaces;
using Garrard.Mcp.Explorer.Infrastructure.Mcp;
using Microsoft.Extensions.Logging;

namespace Garrard.Mcp.Explorer.Infrastructure.Workflows;

/// <summary>
/// Manages workflow definitions and executes them step-by-step via MCP tool calls.
/// </summary>
public sealed class WorkflowService : IWorkflowService
{
    private const int MaxHistoryPerWorkflow = 10;

    private readonly ConnectionService _connectionService;
    private readonly IUserPreferencesStore _store;
    private readonly ILogger<WorkflowService> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public WorkflowService(
        ConnectionService connectionService,
        IUserPreferencesStore store,
        ILogger<WorkflowService> logger)
    {
        _connectionService = connectionService;
        _store = store;
        _logger = logger;
    }

    public async Task<List<WorkflowDefinition>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var prefs = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
        return [.. prefs.Workflows];
    }

    public async Task<WorkflowDefinition?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        var all = await GetAllAsync(cancellationToken).ConfigureAwait(false);
        return all.FirstOrDefault(w => w.Id == id);
    }

    public async Task<WorkflowDefinition> CreateAsync(WorkflowDefinition workflow, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workflow);

        var prefs = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
        var newWorkflow = workflow with
        {
            Id = string.IsNullOrWhiteSpace(workflow.Id) ? Guid.NewGuid().ToString() : workflow.Id,
            CreatedUtc = DateTime.UtcNow,
            ModifiedUtc = DateTime.UtcNow
        };

        var updated = prefs with { Workflows = [.. prefs.Workflows, newWorkflow] };
        await _store.SaveAsync(updated, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Created workflow {Id}: {Name}", newWorkflow.Id, newWorkflow.Name);
        return newWorkflow;
    }

    public async Task<WorkflowDefinition> UpdateAsync(WorkflowDefinition workflow, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentException.ThrowIfNullOrWhiteSpace(workflow.Id);

        var prefs = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
        var list = prefs.Workflows.ToList();
        var idx = list.FindIndex(w => w.Id == workflow.Id);
        if (idx < 0) throw new InvalidOperationException($"Workflow '{workflow.Id}' not found.");

        var updated = workflow with { ModifiedUtc = DateTime.UtcNow };
        list[idx] = updated;
        await _store.SaveAsync(prefs with { Workflows = list }, cancellationToken).ConfigureAwait(false);
        return updated;
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var prefs = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
        var workflows = prefs.Workflows.Where(w => w.Id != id).ToList();
        var executions = prefs.WorkflowExecutions.Where(e => e.WorkflowId != id).ToList();
        await _store.SaveAsync(prefs with { Workflows = workflows, WorkflowExecutions = executions }, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<WorkflowExecution> ExecuteAsync(
        string workflowId,
        string connectionName,
        Dictionary<string, string>? runtimeParameters = null,
        Action<int, int, string>? progressCallback = null,
        bool saveToHistory = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowId);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionName);

        var workflow = await GetByIdAsync(workflowId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Workflow '{workflowId}' not found.");

        var ctx = await EnsureConnectionAsync(connectionName, cancellationToken).ConfigureAwait(false);

        var stepResults = new List<WorkflowStepResult>();

        var execution = new WorkflowExecution
        {
            WorkflowId = workflow.Id,
            WorkflowName = workflow.Name,
            ConnectionName = connectionName,
            StartedUtc = DateTime.UtcNow,
            Status = WorkflowExecutionStatus.Running
        };

        try
        {
            for (int i = 0; i < workflow.Steps.Count; i++)
            {
                var step = workflow.Steps[i];
                progressCallback?.Invoke(i + 1, workflow.Steps.Count, step.ToolName);

                var stepResult = await ExecuteStepAsync(step, ctx, stepResults, runtimeParameters, cancellationToken)
                    .ConfigureAwait(false);
                stepResults.Add(stepResult);

                if (!stepResult.Success && step.ErrorHandling == ErrorHandlingMode.StopOnError)
                {
                    _logger.LogWarning("Workflow {Id} stopped at step {N} due to error", workflowId, step.StepNumber);
                    break;
                }
            }

            var allOk = stepResults.All(r => r.Success);
            var anyFailed = stepResults.Any(r => !r.Success);

            execution = execution with
            {
                CompletedUtc = DateTime.UtcNow,
                Status = allOk ? WorkflowExecutionStatus.Completed :
                         anyFailed && stepResults.Any(r => r.Success) ? WorkflowExecutionStatus.PartiallyCompleted :
                         WorkflowExecutionStatus.Failed,
                StepResults = stepResults
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Workflow {Id} execution failed", workflowId);
            execution = execution with
            {
                CompletedUtc = DateTime.UtcNow,
                Status = WorkflowExecutionStatus.Failed,
                ErrorMessage = ex.Message,
                StepResults = stepResults
            };
        }

        if (saveToHistory)
            await SaveExecutionAsync(execution, cancellationToken).ConfigureAwait(false);

        return execution;
    }

    public async Task<List<WorkflowExecution>> GetExecutionHistoryAsync(
        string workflowId, int limit = 10, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowId);
        var prefs = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
        return prefs.WorkflowExecutions
            .Where(e => e.WorkflowId == workflowId)
            .OrderByDescending(e => e.StartedUtc)
            .Take(limit)
            .ToList();
    }

    public async Task<List<WorkflowExecution>> GetAllExecutionHistoryAsync(
        int limit = 10, CancellationToken cancellationToken = default)
    {
        var prefs = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
        return prefs.WorkflowExecutions
            .OrderByDescending(e => e.StartedUtc)
            .Take(limit)
            .ToList();
    }

    public async Task DeleteExecutionAsync(string executionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
        var prefs = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
        var executions = prefs.WorkflowExecutions.Where(e => e.Id != executionId).ToList();
        await _store.SaveAsync(prefs with { WorkflowExecutions = executions }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LoadTestResult> RunLoadTestAsync(
        string workflowId,
        string connectionName,
        int durationSeconds,
        int maxParallelExecutions,
        Dictionary<string, string>? runtimeParameters = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowId);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionName);

        var started = DateTime.UtcNow;
        var endTime = started.AddSeconds(durationSeconds);

        int totalRequests = 0, successful = 0, failed = 0;
        var durations = new System.Collections.Concurrent.ConcurrentBag<double>();

        using var durationCts = new CancellationTokenSource(TimeSpan.FromSeconds(durationSeconds));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, durationCts.Token);

        var semaphore = new SemaphoreSlim(maxParallelExecutions);
        var tasks = new List<Task>();

        while (!linked.Token.IsCancellationRequested && DateTime.UtcNow < endTime)
        {
            await semaphore.WaitAsync(linked.Token).ConfigureAwait(false);

            Interlocked.Increment(ref totalRequests);
            var sw = System.Diagnostics.Stopwatch.StartNew();

            var t = Task.Run(async () =>
            {
                try
                {
                    await ExecuteAsync(workflowId, connectionName, runtimeParameters,
                        null, false, linked.Token).ConfigureAwait(false);
                    Interlocked.Increment(ref successful);
                    durations.Add(sw.Elapsed.TotalMilliseconds);
                }
                catch
                {
                    Interlocked.Increment(ref failed);
                }
                finally
                {
                    semaphore.Release();
                }
            }, linked.Token);

            tasks.Add(t);
        }

        try { await Task.WhenAll(tasks).ConfigureAwait(false); } catch { /* ignore */ }

        var completed = DateTime.UtcNow;
        var elapsed = (completed - started).TotalSeconds;
        var sortedDurations = durations.OrderBy(d => d).ToList();

        double Percentile(double p) => sortedDurations.Count == 0 ? 0 :
            sortedDurations[Math.Min(sortedDurations.Count - 1, (int)(sortedDurations.Count * p / 100.0))];

        return new LoadTestResult
        {
            WorkflowId = workflowId,
            StartedUtc = started,
            CompletedUtc = completed,
            TotalRequests = totalRequests,
            SuccessfulRequests = successful,
            FailedRequests = failed,
            RequestsPerSecond = elapsed > 0 ? totalRequests / elapsed : 0,
            AverageResponseMs = sortedDurations.Count > 0 ? sortedDurations.Average() : 0,
            P50ResponseMs = Percentile(50),
            P90ResponseMs = Percentile(90),
            P99ResponseMs = Percentile(99),
            ErrorRate = totalRequests > 0 ? (double)failed / totalRequests : 0
        };
    }

    public string ExportToJson(WorkflowDefinition workflow)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        return JsonSerializer.Serialize(workflow, JsonOpts);
    }

    public WorkflowDefinition? ImportFromJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        try
        {
            var w = JsonSerializer.Deserialize<WorkflowDefinition>(json);
            if (w is not null)
                w = w with { Id = Guid.NewGuid().ToString(), CreatedUtc = DateTime.UtcNow, ModifiedUtc = DateTime.UtcNow };
            return w;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import workflow from JSON");
            return null;
        }
    }

    // ── private helpers ──────────────────────────────────────────────────────

    private async Task<ConnectionContext> EnsureConnectionAsync(string connectionName, CancellationToken ct)
    {
        var ctx = _connectionService.GetConnectionContext(connectionName);
        if (ctx is not null) return ctx;

        var prefs = await _store.LoadAsync(ct).ConfigureAwait(false);
        var def = prefs.Connections.FirstOrDefault(c =>
            string.Equals(c.Name, connectionName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Connection '{connectionName}' not found in preferences.");

        var active = await _connectionService.ConnectAsync(def, ct).ConfigureAwait(false);
        ctx = _connectionService.GetConnectionContext(connectionName)
            ?? throw new InvalidOperationException($"Failed to connect to '{connectionName}'.");
        return ctx;
    }

    private async Task<WorkflowStepResult> ExecuteStepAsync(
        WorkflowStep step,
        ConnectionContext ctx,
        List<WorkflowStepResult> previousResults,
        Dictionary<string, string>? runtimeParameters,
        CancellationToken ct)
    {
        var result = new WorkflowStepResult
        {
            StepNumber = step.StepNumber,
            ToolName = step.ToolName,
            StartedUtc = DateTime.UtcNow
        };

        try
        {
            var parameters = BuildParameters(step, previousResults, runtimeParameters);

            var toolResult = await ctx.Client.CallToolAsync(
                step.ToolName,
                (IReadOnlyDictionary<string, object?>)parameters.ToDictionary(k => k.Key, k => (object?)k.Value),
                cancellationToken: ct)
                .ConfigureAwait(false);

            var json = JsonSerializer.Serialize(toolResult.Content, JsonOpts);
            result.Success = true;
            result.Result = JsonNode.Parse(json) ?? json;
            result.CompletedUtc = DateTime.UtcNow;

            _logger.LogInformation("Step {N} ({Tool}) succeeded", step.StepNumber, step.ToolName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Step {N} ({Tool}) failed", step.StepNumber, step.ToolName);
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.CompletedUtc = DateTime.UtcNow;
        }

        return result;
    }

    private static Dictionary<string, object> BuildParameters(
        WorkflowStep step,
        List<WorkflowStepResult> previousResults,
        Dictionary<string, string>? runtimeParameters)
    {
        var parameters = new Dictionary<string, object>();

        foreach (var mapping in step.ParameterMappings)
        {
            object? value = null;

            if (!string.IsNullOrEmpty(mapping.Value))
            {
                // Static / runtime value
                if (runtimeParameters?.TryGetValue(mapping.ParameterName, out var rv) == true)
                    value = rv;
                else
                    value = mapping.Value;
            }
            else if (mapping.SourceStepNumber.HasValue)
            {
                // Extract from previous step result
                var stepIdx = mapping.SourceStepNumber.Value - 1;
                if (stepIdx >= 0 && stepIdx < previousResults.Count)
                {
                    var prev = previousResults[stepIdx];
                    value = ExtractProperty(prev.Result, mapping.SourcePropertyName);
                }
            }

            if (value is not null)
                parameters[mapping.ParameterName] = value;
        }

        return parameters;
    }

    private static object? ExtractProperty(object? result, string? propertyName)
    {
        if (result is null) return null;
        if (string.IsNullOrWhiteSpace(propertyName)) return result?.ToString();

        try
        {
            var node = result is JsonNode jn ? jn : JsonNode.Parse(result.ToString() ?? "null");
            if (node is null) return null;

            var parts = propertyName.Split('.', StringSplitOptions.RemoveEmptyEntries);
            JsonNode? current = node;
            foreach (var part in parts)
            {
                if (current is JsonObject obj && obj.TryGetPropertyValue(part, out var next))
                    current = next;
                else
                    return null;
            }

            return current is JsonValue v ? v.ToString() : current?.ToJsonString();
        }
        catch
        {
            return null;
        }
    }

    private async Task SaveExecutionAsync(WorkflowExecution execution, CancellationToken ct)
    {
        var prefs = await _store.LoadAsync(ct).ConfigureAwait(false);
        var executions = prefs.WorkflowExecutions.ToList();
        executions.Add(execution);

        // Trim per-workflow history
        var toRemove = executions
            .Where(e => e.WorkflowId == execution.WorkflowId)
            .OrderByDescending(e => e.StartedUtc)
            .Skip(MaxHistoryPerWorkflow)
            .ToList();
        foreach (var old in toRemove)
            executions.Remove(old);

        await _store.SaveAsync(prefs with { WorkflowExecutions = executions }, ct).ConfigureAwait(false);
    }
}
