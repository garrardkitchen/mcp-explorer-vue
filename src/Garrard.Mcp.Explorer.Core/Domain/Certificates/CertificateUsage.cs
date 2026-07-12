namespace Garrard.Mcp.Explorer.Core.Domain.Certificates;

/// <summary>Which saved connections and HTTP API definitions reference a certificate.</summary>
public sealed record CertificateUsage
{
    public string CertificateName { get; init; } = string.Empty;
    public IReadOnlyList<string> ConnectionNames { get; init; } = [];
    public IReadOnlyList<string> HttpApiNames { get; init; } = [];

    public bool IsInUse => ConnectionNames.Count > 0 || HttpApiNames.Count > 0;
}
