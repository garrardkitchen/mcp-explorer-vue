namespace Garrard.Mcp.Explorer.Core.Domain.Connections;

public sealed record AzureClientCredentialsOptions
{
    public string TenantId { get; init; } = string.Empty;
    public string ClientId { get; init; } = string.Empty;
    public string ClientSecret { get; init; } = string.Empty;
    public string Scope { get; init; } = string.Empty;
    public string? AuthorityHost { get; init; }

    /// <summary>
    /// When set, the <see cref="ClientSecret"/> field is ignored and the secret
    /// is resolved at runtime from Azure Key Vault via <c>DefaultAzureCredential</c>.
    /// </summary>
    public KeyVaultSecretReference? KeyVaultSecretRef { get; init; }
}
