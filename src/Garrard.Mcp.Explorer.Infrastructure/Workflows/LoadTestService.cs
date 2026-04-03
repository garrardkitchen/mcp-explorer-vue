using System.Collections.Concurrent;
using System.Text.Json;
using Garrard.Mcp.Explorer.Core.Domain.Workflows;
using Garrard.Mcp.Explorer.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Garrard.Mcp.Explorer.Infrastructure.Workflows;

/// <summary>
/// Executes and persists load test results for workflows.
/// Results are stored in the platform-specific data directory alongside settings.
/// </summary>
public sealed class LoadTestService
{
    private readonly IWorkflowService _workflowService;
    private readonly ILogger<LoadTestService> _logger;
    private static readonly string ResultsDirectory;

    static LoadTestService()
    {
        var dataDir = OperatingSystem.IsWindows()
            ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");

        ResultsDirectory = Path.Combine(dataDir, "McpExplorer", "load_tests");
        Directory.CreateDirectory(ResultsDirectory);
    }

    public LoadTestService(IWorkflowService workflowService, ILogger<LoadTestService> logger)
    {
        _workflowService = workflowService;
        _logger = logger;
    }

    public async Task<LoadTestResult> RunAsync(
        string workflowId,
        string connectionName,
        int durationSeconds,
        int maxParallelExecutions,
        Dictionary<string, string>? runtimeParameters = null,
        CancellationToken cancellationToken = default)
    {
        return await _workflowService.RunLoadTestAsync(
            workflowId,
            connectionName,
            durationSeconds,
            maxParallelExecutions,
            runtimeParameters,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveResultAsync(LoadTestResult result, string workflowName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);

        var sanitized = string.Join("_", workflowName.Split(Path.GetInvalidFileNameChars()));
        var fileName = $"loadtest_{sanitized}_{result.StartedUtc:yyyyMMdd_HHmmss}.json";
        var filePath = Path.Combine(ResultsDirectory, fileName);

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
        if (!Directory.Exists(ResultsDirectory)) return [];

        var results = new List<LoadTestResult>();
        foreach (var file in Directory.GetFiles(ResultsDirectory, "loadtest_*.json"))
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
