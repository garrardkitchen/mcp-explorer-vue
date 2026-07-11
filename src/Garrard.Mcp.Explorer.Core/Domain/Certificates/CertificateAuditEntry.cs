namespace Garrard.Mcp.Explorer.Core.Domain.Certificates;

/// <summary>One line of the append-only certificate audit log (<c>certs/audit.jsonl</c>).</summary>
public sealed record CertificateAuditEntry
{
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>e.g. <c>create</c>, <c>upload</c>, <c>renew</c>, <c>delete</c>, <c>export-pfx</c>, <c>import</c>, <c>csr</c>.</summary>
    public string Action { get; init; } = string.Empty;

    public string CertificateName { get; init; } = string.Empty;

    public string? Details { get; init; }
}
