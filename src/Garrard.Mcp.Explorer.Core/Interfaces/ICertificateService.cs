using System.Security.Cryptography.X509Certificates;
using Garrard.Mcp.Explorer.Core.Domain.Certificates;

namespace Garrard.Mcp.Explorer.Core.Interfaces;

/// <summary>
/// Manages the local client-certificate store (<c>&lt;dataDir&gt;/certs/&lt;name&gt;/</c>).
/// Private keys never leave the store except via password-protected PFX export.
/// </summary>
public interface ICertificateService
{
    /// <summary>Absolute path of the certificate store root directory.</summary>
    string CertificatesDirectory { get; }

    Task<OperationResult> GenerateSelfSignedAsync(GenerateCertificateRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CertificateInfo>> ListAsync(CancellationToken cancellationToken = default);

    Task<CertificateInfo?> GetAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>Deletes a certificate. Throws <see cref="CertificateInUseException"/> while referenced.</summary>
    Task DeleteAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>Which saved connections / HTTP API definitions reference the certificate.</summary>
    Task<CertificateUsage> GetUsageAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>The public certificate as PEM (no key material).</summary>
    Task<string> GetPublicCertPemAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>Exports a password-protected PFX bundle. Password is mandatory; the export is audit-logged.</summary>
    Task<byte[]> ExportPfxAsync(string name, string password, CancellationToken cancellationToken = default);

    /// <summary>Loads the certificate with its private key for token acquisition. Never exposed via the API.</summary>
    Task<X509Certificate2> LoadWithPrivateKeyAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>Records a successful public-key upload to an App Registration.</summary>
    Task RecordUploadAsync(string name, CertificateUploadRecord record, CancellationToken cancellationToken = default);

    /// <summary>Removes an upload record (after the key credential is deleted in Azure).</summary>
    Task RemoveUploadRecordAsync(string name, string keyId, CancellationToken cancellationToken = default);

    /// <summary>Marks a certificate as superseded by its renewal successor.</summary>
    Task MarkSupersededAsync(string name, string renewedBy, CancellationToken cancellationToken = default);

    /// <summary>Certificates expiring within <paramref name="days"/> (including already expired), excluding superseded ones.</summary>
    Task<IReadOnlyList<CertificateInfo>> GetExpiringAsync(int days = 30, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CertificateAuditEntry>> ReadAuditAsync(string? name = null, int limit = 200, CancellationToken cancellationToken = default);

    // ── CSR flow (CA-issued certificates) ────────────────────────────────────

    /// <summary>Creates a private key + CSR; the certificate stays <see cref="CertificateState.CsrPending"/> until issued.</summary>
    Task<OperationResult> CreateCsrAsync(CreateCsrRequest request, CancellationToken cancellationToken = default);

    Task<string> GetCsrPemAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>Imports the CA-issued certificate for a pending CSR; validates it matches the stored private key.</summary>
    Task<OperationResult> ImportIssuedCertificateAsync(string name, string certificatePem, CancellationToken cancellationToken = default);

    // ── Import (export/import bundles, Key Vault) ────────────────────────────

    /// <summary>Imports a certificate + private key into the store (used by export/import and Key Vault import).</summary>
    Task<CertificateInfo> ImportAsync(
        string name,
        string certificatePem,
        string privateKeyPem,
        byte[]? pfxBytes,
        CertificateSource source,
        CancellationToken cancellationToken = default);
}
