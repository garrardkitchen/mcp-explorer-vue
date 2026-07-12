using Garrard.Mcp.Explorer.Core.Domain.Certificates;

namespace Garrard.Mcp.Explorer.Core.Interfaces;

/// <summary>
/// Moves certificates between the local store and Azure Key Vault so teams can share
/// them without copying private key files around.
/// </summary>
public interface IKeyVaultCertificateService
{
    /// <summary>Lists enabled certificate names in the vault.</summary>
    Task<IReadOnlyList<string>> ListCertificatesAsync(string vaultName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a certificate (with its private key — requires an exportable policy)
    /// from Key Vault into the local store.
    /// </summary>
    Task<OperationResult> ImportFromKeyVaultAsync(string vaultName, string kvCertificateName, string localName, CancellationToken cancellationToken = default);

    /// <summary>Imports a local certificate into Key Vault as a PFX.</summary>
    Task<OperationResult> ExportToKeyVaultAsync(string localName, string vaultName, CancellationToken cancellationToken = default);
}
