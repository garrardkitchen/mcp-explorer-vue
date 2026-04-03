namespace Garrard.Mcp.Explorer.Core.Domain.Elicitation;

public sealed record ElicitationHistoryEntry(
    ElicitationRequest Request,
    ElicitationResponse? Response);
