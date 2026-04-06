using Garrard.Mcp.Explorer.Core.Domain.Connections;

namespace Garrard.Mcp.Explorer.Core.Interfaces;

/// <summary>Encrypts/decrypts connection bundles for portable export/import.</summary>
public interface IConnectionExportService
{
    /// <summary>Serialises and encrypts <paramref name="connections"/> with <paramref name="password"/>.</summary>
    ConnectionExportPayload Encrypt(IReadOnlyList<ConnectionDefinition> connections, string password);

    /// <summary>
    /// Decrypts and deserialises the payload.
    /// Throws <see cref="InvalidOperationException"/> when the password is wrong.
    /// </summary>
    IReadOnlyList<ConnectionDefinition> Decrypt(ConnectionExportPayload payload, string password);
}

/// <summary>Wire format written to the exported .json file.</summary>
public sealed record ConnectionExportPayload
{
    public int Version { get; init; } = 1;
    public string Salt  { get; init; } = string.Empty; // base64
    public string Nonce { get; init; } = string.Empty; // base64
    public string Data  { get; init; } = string.Empty; // base64(ciphertext + 16-byte GCM auth-tag)
}
