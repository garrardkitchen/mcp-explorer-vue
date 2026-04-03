using System.Text.Json;

namespace Garrard.Mcp.Explorer.Core.Domain.Elicitation;

public sealed record ElicitationResponse(
    string RequestId,
    DateTime TimestampUtc,
    string Action,
    Dictionary<string, JsonElement>? Content);
