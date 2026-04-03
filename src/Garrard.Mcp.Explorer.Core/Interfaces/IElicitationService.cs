using System.Text.Json;
using Garrard.Mcp.Explorer.Core.Domain.Elicitation;

namespace Garrard.Mcp.Explorer.Core.Interfaces;

public interface IElicitationService
{
    IReadOnlyList<ElicitationRequest> GetPendingRequests(string? connectionName = null);
    IReadOnlyList<ElicitationHistoryEntry> GetHistory(string? connectionName = null);
    Task<bool> SubmitResponseAsync(string requestId, string action, Dictionary<string, JsonElement>? content, CancellationToken cancellationToken = default);
    event EventHandler<ElicitationRequestedEventArgs>? ElicitationRequested;
}

public sealed class ElicitationRequestedEventArgs(ElicitationRequest request) : EventArgs
{
    public ElicitationRequest Request { get; } = request;
}
