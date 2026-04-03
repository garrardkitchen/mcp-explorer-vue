namespace Garrard.Mcp.Explorer.Core.Domain.Connections;

public sealed class ConnectionDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public ConnectionAuthenticationMode AuthenticationMode { get; set; } = ConnectionAuthenticationMode.CustomHeaders;
    public List<ConnectionHeader> Headers { get; init; } = [];
    public AzureClientCredentialsOptions? AzureCredentials { get; set; }
    public OAuthConnectionOptions? OAuthOptions { get; set; }
    public string Note { get; set; } = string.Empty;
    public string? GroupName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastUpdatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
}
