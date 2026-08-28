using Garrard.Mcp.Explorer.Core.Domain.Chat;
using Garrard.Mcp.Explorer.Core.Domain.LlmModels;

namespace Garrard.Mcp.Explorer.Core.Interfaces;

public interface IFoundryProjectAgentService
{
    Task<IReadOnlyList<FoundryAgentCatalogItem>> DiscoverAgentsAsync(
        LlmModelDefinition model,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<ChatStreamEvent> StreamAsync(
        string message,
        IReadOnlyList<ChatMessage> history,
        LlmModelDefinition model,
        IReadOnlyList<string> connectionNames,
        CancellationToken cancellationToken = default);

    Task<string> TestAsync(LlmModelDefinition model, CancellationToken cancellationToken = default);
}

public sealed record FoundryAgentCatalogItem(
    string Name,
    IReadOnlyList<FoundryAgentVersionCatalogItem> Versions);

public sealed record FoundryAgentVersionCatalogItem(
    string Version,
    string SystemPrompt,
    string Description,
    DateTimeOffset CreatedAt);
