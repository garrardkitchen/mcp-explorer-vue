namespace Garrard.Mcp.Explorer.Core.Domain.HttpApi;

public sealed class HttpApiAzureCredentialsOptions
{
    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public string? AuthorityHost { get; set; }
}
