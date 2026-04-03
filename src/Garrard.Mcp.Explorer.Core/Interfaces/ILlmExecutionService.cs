using Garrard.Mcp.Explorer.Core.Domain.LlmModels;

namespace Garrard.Mcp.Explorer.Core.Interfaces;

public interface ILlmExecutionService
{
    Task<string> ExecutePromptAsync(LlmModelDefinition model, string userMessage, CancellationToken cancellationToken = default);
}
