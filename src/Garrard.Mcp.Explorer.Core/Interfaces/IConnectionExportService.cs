using Garrard.Mcp.Explorer.Core.Domain.Certificates;
using Garrard.Mcp.Explorer.Core.Domain.Connections;

namespace Garrard.Mcp.Explorer.Core.Interfaces;

/// <summary>Connections plus any bundled certificates decrypted from an export payload.</summary>
public sealed record ConnectionExportBundle
{
    public IReadOnlyList<ConnectionDefinition> Connections { get; init; } = [];
    public IReadOnlyList<ExportedCertificate> Certificates { get; init; } = [];
}

/// <summary>Encrypts/decrypts connection bundles for portable export/import.</summary>
public interface IConnectionExportService
{
    /// <summary>Serialises and encrypts <paramref name="connections"/> with <paramref name="password"/>.</summary>
    ConnectionExportPayload Encrypt(IReadOnlyList<ConnectionDefinition> connections, string password);

    /// <summary>
    /// Serialises and encrypts connections together with referenced certificates
    /// (private keys travel only inside the encrypted payload).
    /// </summary>
    ConnectionExportPayload Encrypt(IReadOnlyList<ConnectionDefinition> connections, IReadOnlyList<ExportedCertificate> certificates, string password);

    /// <summary>
    /// Decrypts and deserialises the payload.
    /// Throws <see cref="InvalidOperationException"/> when the password is wrong.
    /// </summary>
    IReadOnlyList<ConnectionDefinition> Decrypt(ConnectionExportPayload payload, string password);

    /// <summary>Decrypts either format: legacy connection arrays or v2 bundles with certificates.</summary>
    ConnectionExportBundle DecryptBundle(ConnectionExportPayload payload, string password);
}

/// <summary>Wire format written to the exported .json file.</summary>
public sealed record ConnectionExportPayload
{
    public int Version { get; init; } = 1;
    public string Salt  { get; init; } = string.Empty; // base64
    public string Nonce { get; init; } = string.Empty; // base64
    public string Data  { get; init; } = string.Empty; // base64(ciphertext + 16-byte GCM auth-tag)

    /// <summary>PBKDF2 iteration count used to derive the key. Null in files exported before the field existed (100,000).</summary>
    public int? Iterations { get; init; }
}
