namespace Garrard.Mcp.Explorer.Core.Domain.Connections;

public sealed class OAuthConnectionOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string? ClientSecret { get; set; }
    public string RedirectUri { get; set; } = string.Empty;
    public string Scopes { get; set; } = string.Empty;
    public string? ClientMetadataDocumentUri { get; set; }

    /// <summary>
    /// When set, <see cref="ClientSecret"/> is ignored and the secret is resolved
    /// at runtime from Azure Key Vault via <c>DefaultAzureCredential</c>.
    /// </summary>
    public KeyVaultSecretReference? KeyVaultSecretRef { get; set; }
}
