namespace Garrard.Mcp.Explorer.Core.Domain.Certificates;

/// <summary>
/// Thrown when deleting a certificate that is still referenced by saved connections
/// or HTTP API definitions. Carries the usage so callers can report what blocks the delete.
/// </summary>
public sealed class CertificateInUseException(CertificateUsage usage)
    : InvalidOperationException($"Certificate '{usage.CertificateName}' is referenced by " +
                                $"{usage.ConnectionNames.Count} connection(s) and {usage.HttpApiNames.Count} HTTP API definition(s).")
{
    public CertificateUsage Usage { get; } = usage;
}
