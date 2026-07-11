namespace Garrard.Mcp.Explorer.Core.Domain.Certificates;

/// <summary>A certificate upload whose remote key credential no longer matches the local store.</summary>
public sealed record StaleUploadInfo(string CertificateName, string AppId, string DisplayName, string Status);

/// <summary>
/// Snapshot produced by the certificate expiry monitor: certificates needing attention
/// and any uploads detected as stale during the best-effort Graph verification pass.
/// </summary>
public sealed record CertificateNotificationsSnapshot
{
    public DateTimeOffset? LastCheckedAt { get; init; }
    public IReadOnlyList<CertificateInfo> Expiring { get; init; } = [];
    public IReadOnlyList<CertificateInfo> Expired { get; init; } = [];
    public IReadOnlyList<StaleUploadInfo> StaleUploads { get; init; } = [];
}
