using Garrard.Mcp.Explorer.Core.Domain.Certificates;

namespace Garrard.Mcp.Explorer.Core.Interfaces;

/// <summary>Live upload status of a local certificate on an App Registration.</summary>
public enum UploadStatus
{
    /// <summary>A key credential matching the local certificate's thumbprint exists.</summary>
    Current = 0,

    /// <summary>The recorded key credential exists but no longer matches the local certificate (or has expired).</summary>
    Stale = 1,

    /// <summary>No matching key credential exists on the app registration.</summary>
    Missing = 2,
}

/// <summary>A keyCredential on an App Registration, annotated with local-store knowledge.</summary>
public sealed record GraphKeyCredentialInfo(
    string KeyId,
    string? DisplayName,
    string? CustomKeyIdentifierHex,
    DateTimeOffset? StartDateTime,
    DateTimeOffset? EndDateTime,
    /// <summary>Name of the local certificate whose SHA-1 thumbprint matches, if any.</summary>
    string? LocalCertificateName,
    /// <summary>Expired in Azure, or matches a local certificate that has been superseded.</summary>
    bool IsStale,
    /// <summary>Human-readable reason when <see cref="IsStale"/> is true, e.g. "superseded by finance-cert-r2" or "expired 2026-03-14".</summary>
    string? StaleReason = null);

/// <summary>
/// Azure-facing certificate operations: uploading public keys to App Registrations via
/// Microsoft Graph, inspecting/removing keyCredentials, and dry-run token acquisition.
/// Only public certificate material ever leaves the machine.
/// </summary>
public interface ICertificateUploadService
{
    /// <summary>
    /// Uploads the certificate's public key to the app registration as an
    /// <c>AsymmetricX509Cert</c> keyCredential. Idempotent: if a credential with the same
    /// thumbprint already exists the upload step is skipped and the record refreshed.
    /// </summary>
    Task<OperationResult> UploadAsync(string certificateName, string appId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GraphKeyCredentialInfo>> ListKeyCredentialsAsync(string appId, CancellationToken cancellationToken = default);

    /// <summary>Removes a keyCredential from the app registration (stale-credential cleanup).</summary>
    Task RemoveKeyCredentialAsync(string appId, string keyId, CancellationToken cancellationToken = default);

    /// <summary>Compares the local certificate against the app registration's keyCredentials.</summary>
    Task<UploadStatus> VerifyUploadAsync(string certificateName, string appId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Dry-run token acquisition with <c>ClientCertificateCredential</c>. Failure messages include
    /// a hint about the 30-60s Entra ID propagation delay after a fresh upload.
    /// </summary>
    Task<OperationResult> TestTokenAsync(string certificateName, string tenantId, string clientId, string scope, CancellationToken cancellationToken = default);
}
