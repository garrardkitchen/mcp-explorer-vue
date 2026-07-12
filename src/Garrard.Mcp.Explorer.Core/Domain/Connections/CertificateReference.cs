namespace Garrard.Mcp.Explorer.Core.Domain.Connections;

/// <summary>
/// A reference to a client certificate in the local certificate store
/// (<c>&lt;dataDir&gt;/certs/&lt;name&gt;/</c>). Only the name is persisted; the
/// certificate and private key are loaded from disk at token-acquisition time,
/// so renewing a certificate is a single repoint of this reference.
/// </summary>
public sealed record CertificateReference
{
    /// <summary>The certificate name in the local store (e.g. <c>mcp-finance-cert</c>).</summary>
    public string CertificateName { get; init; } = string.Empty;

    public override string ToString() => $"cert:{CertificateName}";
}
