namespace Garrard.Mcp.Explorer.Core.Domain.Elicitation;

public sealed record ElicitationRequest(
    string Id,
    string ConnectionName,
    DateTime TimestampUtc,
    string? Message,
    Dictionary<string, object> Schema,
    ElicitationStatus Status);
