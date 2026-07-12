namespace Garrard.Mcp.Explorer.Core.Domain.Certificates;

/// <summary>
/// A certificate carried inside a password-protected export bundle. The private key is
/// present only inside the AES-256-GCM encrypted payload — never in plaintext output.
/// </summary>
public sealed record ExportedCertificate
{
    public string Name { get; init; } = string.Empty;
    public string CertPem { get; init; } = string.Empty;
    public string KeyPem { get; init; } = string.Empty;
    public string? PfxBase64 { get; init; }
    public CertificateSource Source { get; init; } = CertificateSource.SelfSigned;
}
