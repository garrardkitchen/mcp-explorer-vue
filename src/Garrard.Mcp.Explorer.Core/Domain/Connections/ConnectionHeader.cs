namespace Garrard.Mcp.Explorer.Core.Domain.Connections;

public sealed class ConnectionHeader
{
    public string Name { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public string AuthorizationType { get; set; } = string.Empty;
    public bool IsAuthorization => string.Equals(Name, "Authorization", StringComparison.OrdinalIgnoreCase);
}
