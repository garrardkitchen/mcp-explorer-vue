using Garrard.Mcp.Explorer.Core.Domain.Workflows;

namespace Garrard.Mcp.Explorer.Core.Interfaces;

public interface IWorkflowService
{
    Task<List<WorkflowDefinition>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<WorkflowDefinition?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<WorkflowDefinition> CreateAsync(WorkflowDefinition workflow, CancellationToken cancellationToken = default);
    Task<WorkflowDefinition> UpdateAsync(WorkflowDefinition workflow, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
    Task<WorkflowExecution> ExecuteAsync(string workflowId, string connectionName, Dictionary<string, string>? runtimeParameters = null, Action<int, int, string>? progressCallback = null, bool saveToHistory = true, CancellationToken cancellationToken = default);
    Task<List<WorkflowExecution>> GetExecutionHistoryAsync(string workflowId, int limit = 10, CancellationToken cancellationToken = default);
    Task<List<WorkflowExecution>> GetAllExecutionHistoryAsync(int limit = 10, CancellationToken cancellationToken = default);
    Task DeleteExecutionAsync(string executionId, CancellationToken cancellationToken = default);
    Task<LoadTestResult> RunLoadTestAsync(string workflowId, string connectionName, int durationSeconds, int maxParallelExecutions, Dictionary<string, string>? runtimeParameters = null, Action<LoadTestProgress>? progressCallback = null, CancellationToken cancellationToken = default);
    string ExportToJson(WorkflowDefinition workflow);
    WorkflowDefinition? ImportFromJson(string json);
}
