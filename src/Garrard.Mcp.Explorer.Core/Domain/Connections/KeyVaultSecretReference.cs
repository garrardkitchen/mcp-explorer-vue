namespace Garrard.Mcp.Explorer.Core.Domain.Connections;

/// <summary>
/// A reference to an Azure Key Vault secret. The actual secret value is never
/// stored; it is resolved at runtime via <c>DefaultAzureCredential</c>.
/// </summary>
public sealed record KeyVaultSecretReference
{
    /// <summary>The Key Vault name (e.g. <c>kv-prod-uksouth</c>).</summary>
    public string VaultName { get; init; } = string.Empty;

    /// <summary>The secret name inside the vault (e.g. <c>api-client-secret</c>).</summary>
    public string SecretName { get; init; } = string.Empty;

    public override string ToString() => $"{VaultName}/{SecretName}";
}
