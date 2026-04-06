using System.Collections.Concurrent;
using System.Text.Json;
using Garrard.Mcp.Explorer.Core.Domain.Workflows;
using Garrard.Mcp.Explorer.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Garrard.Mcp.Explorer.Infrastructure.Workflows;

/// <summary>
/// Executes and persists load test results for workflows.
/// Results are stored in a load_tests subdirectory alongside the settings file,
/// so they respect the mounted volume path (PREFERENCES__StoragePath).
/// </summary>
public sealed class LoadTestService
{
    private readonly IWorkflowService _workflowService;
    private readonly ILogger<LoadTestService> _logger;
    private readonly string _resultsDirectory;

    // In-memory progress store — keyed by runId, expires on completion retrieval
    private static readonly ConcurrentDictionary<string, LoadTestProgress> _progressStore = new();

    public LoadTestService(IWorkflowService workflowService, IUserPreferencesStore preferencesStore, ILogger<LoadTestService> logger)
    {
        _workflowService = workflowService;
        _logger = logger;

        // Derive the load_tests directory from the same base directory as settings.json
        var baseDir = Path.GetDirectoryName(preferencesStore.StoragePath)
                      ?? Path.Combine(Path.GetTempPath(), "McpExplorer");
        _resultsDirectory = Path.Combine(baseDir, "load_tests");
        Directory.CreateDirectory(_resultsDirectory);

        _logger.LogInformation("Load test results directory: {Path}", _resultsDirectory);
    }

    /// <summary>Starts a load test in the background and returns a runId for progress polling.</summary>
    public string StartAsync(
        string workflowId,
        string connectionName,
        int durationSeconds,
        int maxParallelExecutions,
        string workflowName,
        Dictionary<string, string>? runtimeParameters = null,
        CancellationToken cancellationToken = default)
    {
        var runId = Guid.NewGuid().ToString();
        _progressStore[runId] = new LoadTestProgress { RunId = runId, IsComplete = false };

        _ = Task.Run(async () =>
        {
            try
            {
                // Use CancellationToken.None — the HTTP request token is cancelled once the
                // response is sent (returning the runId), which would abort a long-running
                // background load test and prevent results from being saved.
                var result = await _workflowService.RunLoadTestAsync(
                    workflowId, connectionName, durationSeconds, maxParallelExecutions,
                    runtimeParameters,
                    progress => _progressStore[runId] = progress with { RunId = runId },
                    CancellationToken.None).ConfigureAwait(false);

                await SaveResultAsync(result, workflowName, CancellationToken.None).ConfigureAwait(false);

                _progressStore[runId] = new LoadTestProgress
                {
                    RunId = runId,
                    IsComplete = true,
                    PercentComplete = 100,
                    TotalExecutions = result.TotalRequests,
                    SuccessfulExecutions = result.SuccessfulRequests,
                    FailedExecutions = result.FailedRequests,
                    ActiveExecutions = 0,
                    Result = result
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Load test {RunId} failed", runId);
                _progressStore[runId] = new LoadTestProgress { RunId = runId, IsComplete = true, PercentComplete = 100 };
            }
        }, CancellationToken.None);

        return runId;
    }

    public LoadTestProgress? GetProgress(string runId)
    {
        _progressStore.TryGetValue(runId, out var progress);
        // Clean up completed entries after they are retrieved
        if (progress?.IsComplete == true)
            _progressStore.TryRemove(runId, out _);
        return progress;
    }

    public async Task SaveResultAsync(LoadTestResult result, string workflowName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);

        var sanitized = string.Join("_", workflowName.Split(Path.GetInvalidFileNameChars()));
        var fileName = $"loadtest_{sanitized}_{result.StartedUtc:yyyyMMdd_HHmmss}.json";
        var filePath = Path.Combine(_resultsDirectory, fileName);

        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await File.WriteAllTextAsync(filePath, json, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Saved load test result to {Path}", filePath);
    }

    public async Task<List<LoadTestResult>> ListResultsAsync(
        string workflowId, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_resultsDirectory)) return [];

        var results = new List<LoadTestResult>();
        foreach (var file in Directory.GetFiles(_resultsDirectory, "loadtest_*.json"))
        {
            try
            {
                var json = await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false);
                var result = JsonSerializer.Deserialize<LoadTestResult>(json, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                if (result is not null && result.WorkflowId == workflowId)
                    results.Add(result);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load test result from {File}", file);
            }
        }

        return results.OrderByDescending(r => r.StartedUtc).ToList();
    }
}
