namespace Garrard.Mcp.Explorer.Core.Domain.Connections;

public sealed record AzureClientCredentialsOptions
{
    public string TenantId { get; init; } = string.Empty;
    public string ClientId { get; init; } = string.Empty;
    public string ClientSecret { get; init; } = string.Empty;
    public string Scope { get; init; } = string.Empty;
    public string? AuthorityHost { get; init; }
}
