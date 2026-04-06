using System.Text.Json;
using System.Text.Json.Nodes;
using Garrard.Mcp.Explorer.Core.Domain.Connections;
using Garrard.Mcp.Explorer.Core.Domain.Preferences;
using Garrard.Mcp.Explorer.Core.Domain.Workflows;
using Garrard.Mcp.Explorer.Core.Interfaces;
using Garrard.Mcp.Explorer.Infrastructure.Mcp;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;

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
        Action<LoadTestProgress>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowId);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionName);

        var started = DateTime.UtcNow;
        var endTime = started.AddSeconds(durationSeconds);

        int totalRequests = 0, successful = 0, failed = 0, active = 0;
        var durations = new System.Collections.Concurrent.ConcurrentBag<double>();
        var snapshots = new System.Collections.Concurrent.ConcurrentBag<LoadTestSnapshot>();

        using var durationCts = new CancellationTokenSource(TimeSpan.FromSeconds(durationSeconds));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, durationCts.Token);

        var semaphore = new SemaphoreSlim(maxParallelExecutions);
        var tasks = new List<Task>();

        // Snapshot timer — fires every second
        var snapshotTimer = progressCallback is not null ? Task.Run(async () =>
        {
            while (!linked.Token.IsCancellationRequested)
            {
                await Task.Delay(1000, linked.Token).ConfigureAwait(false);
                var elapsed = (DateTime.UtcNow - started).TotalMilliseconds;
                var pct = Math.Min(100.0, elapsed / (durationSeconds * 1000.0) * 100.0);
                var snap = new LoadTestSnapshot
                {
                    ElapsedMs = elapsed,
                    CumulativeSuccesses = Volatile.Read(ref successful),
                    CumulativeFailures = Volatile.Read(ref failed),
                    ActiveExecutions = Volatile.Read(ref active)
                };
                snapshots.Add(snap);
                progressCallback(new LoadTestProgress
                {
                    IsComplete = false,
                    PercentComplete = pct,
                    TotalExecutions = Volatile.Read(ref totalRequests),
                    SuccessfulExecutions = Volatile.Read(ref successful),
                    FailedExecutions = Volatile.Read(ref failed),
                    ActiveExecutions = Volatile.Read(ref active)
                });
            }
        }, CancellationToken.None) : Task.CompletedTask;

        while (!linked.Token.IsCancellationRequested && DateTime.UtcNow < endTime)
        {
            await semaphore.WaitAsync(linked.Token).ConfigureAwait(false);

            Interlocked.Increment(ref totalRequests);
            Interlocked.Increment(ref active);
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
                    Interlocked.Decrement(ref active);
                    semaphore.Release();
                }
            }, linked.Token);

            tasks.Add(t);
        }

        try { await Task.WhenAll(tasks).ConfigureAwait(false); } catch { /* ignore */ }

        var completed = DateTime.UtcNow;
        var elapsed2 = (completed - started).TotalSeconds;
        var sortedDurations = durations.OrderBy(d => d).ToList();

        double Percentile(double p) => sortedDurations.Count == 0 ? 0 :
            sortedDurations[Math.Min(sortedDurations.Count - 1, (int)(sortedDurations.Count * p / 100.0))];

        var workflow = await GetByIdAsync(workflowId, cancellationToken).ConfigureAwait(false);
        var workflowName = workflow?.Name ?? workflowId;

        return new LoadTestResult
        {
            WorkflowId = workflowId,
            WorkflowName = workflowName,
            ConnectionName = connectionName,
            DurationSeconds = durationSeconds,
            MaxParallelExecutions = maxParallelExecutions,
            StartedUtc = started,
            CompletedUtc = completed,
            TotalRequests = totalRequests,
            SuccessfulRequests = successful,
            FailedRequests = failed,
            RequestsPerSecond = elapsed2 > 0 ? totalRequests / elapsed2 : 0,
            AverageResponseMs = sortedDurations.Count > 0 ? sortedDurations.Average() : 0,
            P50ResponseMs = Percentile(50),
            P90ResponseMs = Percentile(90),
            P99ResponseMs = Percentile(99),
            ErrorRate = totalRequests > 0 ? (double)failed / totalRequests : 0,
            Snapshots = snapshots.OrderBy(s => s.ElapsedMs).ToList()
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
            // PropertyNameCaseInsensitive handles both PascalCase (exported files) and camelCase (settings.json)
            var opts = new JsonSerializerOptions(JsonOpts) { PropertyNameCaseInsensitive = true };
            var w = JsonSerializer.Deserialize<WorkflowDefinition>(json, opts);
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
        // Dispatch to iteration handler if any mapping requests array iteration
        var iterationMapping = step.ParameterMappings.FirstOrDefault(m =>
            m.SourceType == MappingSourceType.FromPreviousStep &&
            m.IterationMode != ArrayIterationMode.None);

        if (iterationMapping is not null)
        {
            return await ExecuteStepWithIterationAsync(step, ctx, previousResults, runtimeParameters, iterationMapping, ct)
                .ConfigureAwait(false);
        }

        return await ExecuteSingleToolCallAsync(step, ctx, previousResults, runtimeParameters, null, ct)
            .ConfigureAwait(false);
    }

    private async Task<WorkflowStepResult> ExecuteStepWithIterationAsync(
        WorkflowStep step,
        ConnectionContext ctx,
        List<WorkflowStepResult> previousResults,
        Dictionary<string, string>? runtimeParameters,
        ParameterMapping iterationMapping,
        CancellationToken ct)
    {
        var startedUtc = DateTime.UtcNow;
        try
        {
            var sourceStepIndex = iterationMapping.SourceStepIndex ?? (previousResults.Count - 1);
            if (previousResults.Count == 0 || sourceStepIndex < 0 || sourceStepIndex >= previousResults.Count)
                throw new InvalidOperationException(
                    $"Cannot iterate: source step index {sourceStepIndex} is invalid. " +
                    "The first workflow step cannot use array iteration because there are no previous step results.");


            var sourceResult = previousResults[sourceStepIndex];
            var parsedOutput = TryParseJson(sourceResult.OutputJson);
            if (parsedOutput is null)
                throw new InvalidOperationException($"Source step {sourceStepIndex} has no parseable output.");

            var arrayElements = ExtractArrayElementsForIteration(parsedOutput, iterationMapping.SourcePropertyPath, iterationMapping.IterationMode);
            if (arrayElements is null || arrayElements.Count == 0)
            {
                _logger.LogWarning("No array elements found for iteration in step {N}", step.StepNumber);
                return new WorkflowStepResult
                {
                    StepNumber = step.StepNumber,
                    ToolName = step.ToolName,
                    Status = StepExecutionStatus.Completed,
                    StartedUtc = startedUtc,
                    CompletedUtc = DateTime.UtcNow,
                    OutputJson = "[]"
                };
            }

            var iterationResults = new List<JsonNode>();
            var allInputs = new List<string>();

            foreach (var element in arrayElements)
            {
                var overrides = new Dictionary<ParameterMapping, object> { { iterationMapping, element } };
                var iterResult = await ExecuteSingleToolCallAsync(step, ctx, previousResults, runtimeParameters, overrides, ct)
                    .ConfigureAwait(false);

                if (!iterResult.Success)
                    return iterResult;

                var parsed = TryParseJson(iterResult.OutputJson);
                if (parsed is not null) iterationResults.Add(parsed);
                if (iterResult.InputJson is not null) allInputs.Add(iterResult.InputJson);
            }

            var combinedArray = new JsonArray(iterationResults
                .Select(n => (JsonNode?)n.DeepClone())
                .ToArray());
            var combinedJson = combinedArray.ToJsonString(JsonOpts);

            _logger.LogInformation("Step {N} ({Tool}) completed {Count} iterations", step.StepNumber, step.ToolName, arrayElements.Count);
            return new WorkflowStepResult
            {
                StepNumber = step.StepNumber,
                ToolName = step.ToolName,
                Status = StepExecutionStatus.Completed,
                StartedUtc = startedUtc,
                CompletedUtc = DateTime.UtcNow,
                InputJson = allInputs.Count > 0 ? $"[{string.Join(",", allInputs)}]" : null,
                OutputJson = combinedJson,
                Result = combinedArray
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Step {N} ({Tool}) iteration failed", step.StepNumber, step.ToolName);
            return new WorkflowStepResult
            {
                StepNumber = step.StepNumber,
                ToolName = step.ToolName,
                Status = StepExecutionStatus.Failed,
                StartedUtc = startedUtc,
                CompletedUtc = DateTime.UtcNow,
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task<WorkflowStepResult> ExecuteSingleToolCallAsync(
        WorkflowStep step,
        ConnectionContext ctx,
        List<WorkflowStepResult> previousResults,
        Dictionary<string, string>? runtimeParameters,
        Dictionary<ParameterMapping, object>? iterationOverrides,
        CancellationToken ct)
    {
        var startedUtc = DateTime.UtcNow;
        try
        {
            var parameters = BuildParameters(step, previousResults, runtimeParameters, iterationOverrides);
            var inputJson = JsonSerializer.Serialize(parameters, JsonOpts);

            var toolResult = await ctx.Client.CallToolAsync(
                step.ToolName,
                (IReadOnlyDictionary<string, object?>)parameters.ToDictionary(k => k.Key, k => (object?)k.Value),
                cancellationToken: ct)
                .ConfigureAwait(false);

            var outputJson = ConvertToolResultToJson(toolResult);
            var parsedNode = TryParseJson(outputJson);

            _logger.LogInformation("Step {N} ({Tool}) succeeded", step.StepNumber, step.ToolName);
            return new WorkflowStepResult
            {
                StepNumber = step.StepNumber,
                ToolName = step.ToolName,
                Status = StepExecutionStatus.Completed,
                StartedUtc = startedUtc,
                CompletedUtc = DateTime.UtcNow,
                InputJson = inputJson,
                OutputJson = outputJson,
                Result = parsedNode ?? (object)outputJson
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Step {N} ({Tool}) failed", step.StepNumber, step.ToolName);
            return new WorkflowStepResult
            {
                StepNumber = step.StepNumber,
                ToolName = step.ToolName,
                Status = StepExecutionStatus.Failed,
                StartedUtc = startedUtc,
                CompletedUtc = DateTime.UtcNow,
                ErrorMessage = ex.Message
            };
        }
    }

    private Dictionary<string, object> BuildParameters(
        WorkflowStep step,
        List<WorkflowStepResult> previousResults,
        Dictionary<string, string>? runtimeParameters,
        Dictionary<ParameterMapping, object>? iterationOverrides = null)
    {
        var parameters = new Dictionary<string, object>();

        foreach (var mapping in step.ParameterMappings)
        {
            if (string.IsNullOrWhiteSpace(mapping.TargetParameter)) continue;

            object? value = null;

            // Iteration overrides take priority (set per-element during Each iteration)
            if (iterationOverrides?.TryGetValue(mapping, out var ov) == true)
            {
                value = ov;
            }
            else
            {
                switch (mapping.SourceType)
                {
                    case MappingSourceType.FromPreviousStep:
                        value = ExtractValueFromPreviousStep(mapping, previousResults);
                        break;

                    case MappingSourceType.ManualValue:
                        value = mapping.ManualValue;
                        break;

                    case MappingSourceType.PromptAtRuntime:
                        if (runtimeParameters?.TryGetValue(mapping.TargetParameter, out var rv) == true)
                            value = rv;
                        break;
                }
            }

            if (value is not null)
                parameters[mapping.TargetParameter] = value;
        }

        return parameters;
    }

    private object? ExtractValueFromPreviousStep(ParameterMapping mapping, List<WorkflowStepResult> previousResults)
    {
        var sourceStepIndex = mapping.SourceStepIndex ?? (previousResults.Count - 1);

        if (sourceStepIndex < 0 || sourceStepIndex >= previousResults.Count)
        {
            _logger.LogWarning("Invalid source step index {SourceStepIndex}", sourceStepIndex);
            return null;
        }

        var sourceResult = previousResults[sourceStepIndex];
        var parsedOutput = sourceResult.Result as JsonNode ?? TryParseJson(sourceResult.OutputJson);

        if (parsedOutput is null)
        {
            _logger.LogWarning("Source step {StepIndex} has no parseable output", sourceStepIndex);
            return null;
        }

        if (string.IsNullOrWhiteSpace(mapping.SourcePropertyPath))
            return sourceResult.OutputJson;

        return ExtractJsonPathValue(parsedOutput, mapping.SourcePropertyPath);
    }

    private object? ExtractJsonPathValue(JsonNode node, string path)
    {
        try
        {
            var parts = ParseJsonPath(path);
            JsonNode? current = node;

            foreach (var part in parts)
            {
                if (current is null) return null;

                if (part.IsArrayIndex)
                {
                    if (current is JsonArray arr && part.ArrayIndex >= 0 && part.ArrayIndex < arr.Count)
                        current = arr[part.ArrayIndex];
                    else
                        return null;
                }
                else
                {
                    if (current is JsonObject obj && obj.TryGetPropertyValue(part.PropertyName!, out var next))
                        current = next;
                    else
                        return null;
                }
            }

            return ConvertJsonNodeToValue(current);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract JSON path {Path}", path);
            return null;
        }
    }

    private static List<JsonPathPart> ParseJsonPath(string path)
    {
        var parts = new List<JsonPathPart>();
        foreach (var segment in path.Split('.'))
        {
            var bracketIdx = segment.IndexOf('[');
            if (bracketIdx >= 0)
            {
                var closeIdx = segment.IndexOf(']');
                if (closeIdx > bracketIdx)
                {
                    if (bracketIdx > 0)
                        parts.Add(new JsonPathPart { PropertyName = segment[..bracketIdx] });

                    var indexStr = segment[(bracketIdx + 1)..closeIdx];
                    if (string.IsNullOrEmpty(indexStr))
                        // Empty brackets [] = array iteration marker
                        parts.Add(new JsonPathPart { IsArrayIndex = true, IsIterationMarker = true });
                    else if (int.TryParse(indexStr, out var idx))
                        parts.Add(new JsonPathPart { IsArrayIndex = true, ArrayIndex = idx });
                }
            }
            else if (!string.IsNullOrEmpty(segment))
            {
                parts.Add(new JsonPathPart { PropertyName = segment });
            }
        }
        return parts;
    }

    private List<object>? ExtractArrayElementsForIteration(JsonNode node, string? path, ArrayIterationMode iterationMode)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                // No path — iterate the root if it's an array
                if (node is JsonArray rootArray)
                    return ApplyIterationModeFilter(rootArray.Where(x => x is not null).Select(x => ConvertJsonNodeToValue(x)!).Where(x => x is not null).ToList(), iterationMode);
                return null;
            }

            var pathParts = ParseJsonPath(path);
            var firstArrayIdx = pathParts.FindIndex(p => p.IsArrayIndex);
            if (firstArrayIdx < 0)
            {
                _logger.LogWarning("Path {Path} contains no array indexing for iteration", path);
                return null;
            }

            // Navigate to the array
            JsonNode? current = node;
            for (int i = 0; i < firstArrayIdx; i++)
            {
                var part = pathParts[i];
                if (current is JsonObject obj && obj.TryGetPropertyValue(part.PropertyName!, out var next))
                    current = next;
                else
                    return null;
            }

            if (current is not JsonArray array)
            {
                _logger.LogWarning("Expected array at path position but found {Type}", current?.GetType().Name ?? "null");
                return null;
            }

            var partsAfter = pathParts.Skip(firstArrayIdx + 1).ToList();
            var elements = new List<object>();

            foreach (var elem in array)
            {
                if (elem is null) continue;
                object? val = partsAfter.Count > 0 ? NavigateJsonPath(elem, partsAfter) : ConvertJsonNodeToValue(elem);
                if (val is not null) elements.Add(val);
            }

            return elements.Count > 0 ? ApplyIterationModeFilter(elements, iterationMode) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract array elements from path {Path}", path);
            return null;
        }
    }

    private object? NavigateJsonPath(JsonNode node, List<JsonPathPart> pathParts)
    {
        JsonNode? current = node;
        foreach (var part in pathParts)
        {
            if (current is null) return null;
            if (part.IsArrayIndex)
            {
                if (current is JsonArray arr && !part.IsIterationMarker && part.ArrayIndex >= 0 && part.ArrayIndex < arr.Count)
                    current = arr[part.ArrayIndex];
                else
                    return null;
            }
            else
            {
                if (current is JsonObject obj && obj.TryGetPropertyValue(part.PropertyName!, out var val))
                    current = val;
                else
                    return null;
            }
        }
        return ConvertJsonNodeToValue(current);
    }

    private static List<object> ApplyIterationModeFilter(List<object> elements, ArrayIterationMode mode) =>
        mode switch
        {
            ArrayIterationMode.First => elements.Take(1).ToList(),
            ArrayIterationMode.Last => elements.Skip(Math.Max(0, elements.Count - 1)).ToList(),
            _ => elements
        };

    private static object? ConvertJsonNodeToValue(JsonNode? node) =>
        node switch
        {
            null => null,
            JsonValue v => v.GetValue<object>(),
            _ => node.ToJsonString()
        };

    private static JsonNode? TryParseJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonNode.Parse(json); }
        catch { return null; }
    }

    private sealed class JsonPathPart
    {
        public string? PropertyName { get; init; }
        public bool IsArrayIndex { get; init; }
        public int ArrayIndex { get; init; }
        /// <summary>True when the bracket was empty <c>[]</c> — marks array iteration, not a fixed index.</summary>
        public bool IsIterationMarker { get; init; }
    }

    private static string ConvertToolResultToJson(CallToolResult toolResult)
        => McpToolResultHelper.ConvertToJson(toolResult);

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
