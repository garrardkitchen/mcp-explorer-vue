using Garrard.Mcp.Explorer.Core.Domain.Connections;

namespace Garrard.Mcp.Explorer.Core.Domain.HttpApi;

public sealed class HttpApiAzureCredentialsOptions
{
    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public string? AuthorityHost { get; set; }
    public KeyVaultSecretReference? KeyVaultSecretRef { get; set; }

    /// <summary>
    /// When set, authentication uses <c>ClientCertificateCredential</c> with a certificate
    /// from the local store. Takes precedence over ClientSecret and KeyVaultSecretRef.
    /// </summary>
    public CertificateReference? CertificateRef { get; set; }

    public string? SubscriptionId { get; set; }
}
