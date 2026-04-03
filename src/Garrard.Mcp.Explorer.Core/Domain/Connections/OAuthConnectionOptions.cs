namespace Garrard.Mcp.Explorer.Core.Domain.Connections;

public sealed class OAuthConnectionOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string? ClientSecret { get; set; }
    public string RedirectUri { get; set; } = string.Empty;
    public string Scopes { get; set; } = string.Empty;
    public string? ClientMetadataDocumentUri { get; set; }
}
