namespace Garrard.Mcp.Explorer.Core.Domain.Certificates;

/// <summary>How a certificate entered the local store.</summary>
public enum CertificateSource
{
    SelfSigned = 0,
    CsrIssued = 1,
    KeyVault = 2,
}

/// <summary>Lifecycle state of a stored certificate.</summary>
public enum CertificateState
{
    Active = 0,

    /// <summary>A CSR + private key exist but the issued certificate has not been imported yet.</summary>
    CsrPending = 1,

    /// <summary>Replaced by a renewal; kept on disk until deleted.</summary>
    Superseded = 2,
}

/// <summary>
/// Record of a public-key upload to an Azure App Registration (Microsoft Graph keyCredential).
/// </summary>
public sealed record CertificateUploadRecord
{
    /// <summary>The application object id (Graph directory object id).</summary>
    public string AppObjectId { get; init; } = string.Empty;

    /// <summary>The application (client) id.</summary>
    public string AppId { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    /// <summary>The Graph keyCredential keyId assigned at upload time.</summary>
    public string KeyId { get; init; } = string.Empty;

    public string UploadedThumbprintSha1 { get; init; } = string.Empty;

    public DateTimeOffset UploadedAt { get; init; }
}

/// <summary>
/// Certificate metadata persisted as <c>metadata.json</c> next to the certificate files.
/// Never contains key material — safe to return from the API as-is.
/// </summary>
public sealed record CertificateInfo
{
    public string Name { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public int KeySize { get; init; }
    public CertificateSource Source { get; init; }
    public CertificateState State { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? NotBefore { get; init; }
    public DateTimeOffset? NotAfter { get; init; }
    public string? ThumbprintSha1 { get; init; }
    public string? ThumbprintSha256 { get; init; }
    public bool HasPfx { get; init; }
    public List<CertificateUploadRecord> UploadedTo { get; init; } = [];

    /// <summary>Name of the successor certificate after a renewal.</summary>
    public string? RenewedBy { get; init; }
}
