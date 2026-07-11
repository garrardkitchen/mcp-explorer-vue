using Garrard.Mcp.Explorer.Core.Domain.HttpApi;

namespace Garrard.Mcp.Explorer.Core.Interfaces;

/// <summary>Encrypts/decrypts HTTP API definition bundles for portable export/import.</summary>
public interface IHttpApiExportService
{
    HttpApiExportPayload Encrypt(IReadOnlyList<HttpApiDefinition> definitions, string password);
    IReadOnlyList<HttpApiDefinition> Decrypt(HttpApiExportPayload payload, string password);
}

/// <summary>Wire format written to the exported .json file.</summary>
public sealed record HttpApiExportPayload
{
    public int Version { get; init; } = 1;
    public string Salt  { get; init; } = string.Empty;
    public string Nonce { get; init; } = string.Empty;
    public string Data  { get; init; } = string.Empty;

    /// <summary>PBKDF2 iteration count used to derive the key. Null in files exported before the field existed (100,000).</summary>
    public int? Iterations { get; init; }
}
