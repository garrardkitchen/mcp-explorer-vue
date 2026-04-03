using Garrard.Mcp.Explorer.Core.Domain.Chat;
using Garrard.Mcp.Explorer.Core.Domain.LlmModels;

namespace Garrard.Mcp.Explorer.Core.Interfaces;

public interface IAiChatService
{
    IAsyncEnumerable<ChatStreamEvent> StreamAsync(
        string message,
        List<ChatMessage> history,
        LlmModelDefinition model,
        IReadOnlyList<string> connectionNames,
        CancellationToken cancellationToken = default);
}
