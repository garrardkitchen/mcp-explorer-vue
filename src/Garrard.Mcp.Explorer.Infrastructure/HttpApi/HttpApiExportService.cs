using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Garrard.Mcp.Explorer.Core.Domain.Certificates;
using Garrard.Mcp.Explorer.Core.Domain.HttpApi;
using Garrard.Mcp.Explorer.Core.Interfaces;

namespace Garrard.Mcp.Explorer.Infrastructure.HttpApi;

/// <summary>
/// AES-256-GCM + PBKDF2-SHA256 encrypt/decrypt for HTTP API definition bundles.
/// Uses the same algorithm as <c>ConnectionExportService</c>.
/// </summary>
public sealed class HttpApiExportService : IHttpApiExportService
{
    private const int KeyBytes    = 32;
    private const int SaltBytes   = 16;
    private const int NonceBytes  = 12;
    private const int TagBytes    = 16;
    private const int Pbkdf2Iters       = 600_000;
    private const int LegacyPbkdf2Iters = 100_000;    // pre-Iterations-field exports; also the minimum accepted on import
    private const int MaxPbkdf2Iters    = 10_000_000; // reject hostile files that claim a huge count to burn CPU on import

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(allowIntegerValues: true) }
    };

    public HttpApiExportPayload Encrypt(IReadOnlyList<HttpApiDefinition> definitions, string password)
        => Encrypt(definitions, [], password);

    public HttpApiExportPayload Encrypt(
        IReadOnlyList<HttpApiDefinition> definitions,
        IReadOnlyList<ExportedCertificate> certificates,
        string password)
    {
        // Without certificates the legacy array format is kept so older builds can import.
        var plaintext = certificates.Count == 0
            ? JsonSerializer.SerializeToUtf8Bytes(definitions, _json)
            : JsonSerializer.SerializeToUtf8Bytes(
                new HttpApiExportBundle { Definitions = definitions, Certificates = certificates }, _json);

        Span<byte> salt  = stackalloc byte[SaltBytes];
        Span<byte> nonce = stackalloc byte[NonceBytes];
        RandomNumberGenerator.Fill(salt);
        RandomNumberGenerator.Fill(nonce);

        var key = DeriveKey(password, salt);
        var cipherBuf = new byte[plaintext.Length + TagBytes];
        var cipher = cipherBuf.AsSpan(0, plaintext.Length);
        var tag    = cipherBuf.AsSpan(plaintext.Length, TagBytes);

        using var aes = new AesGcm(key, TagBytes);
        aes.Encrypt(nonce, plaintext, cipher, tag);

        return new HttpApiExportPayload
        {
            Version    = certificates.Count == 0 ? 1 : 2,
            Salt       = Convert.ToBase64String(salt),
            Nonce      = Convert.ToBase64String(nonce),
            Data       = Convert.ToBase64String(cipherBuf),
            Iterations = Pbkdf2Iters
        };
    }

    public IReadOnlyList<HttpApiDefinition> Decrypt(HttpApiExportPayload payload, string password)
        => DecryptBundle(payload, password).Definitions;

    public HttpApiExportBundle DecryptBundle(HttpApiExportPayload payload, string password)
    {
        try
        {
            var salt     = Convert.FromBase64String(payload.Salt);
            var nonce    = Convert.FromBase64String(payload.Nonce);
            var combined = Convert.FromBase64String(payload.Data);

            if (combined.Length < TagBytes)
                throw new InvalidOperationException("Incorrect password or corrupted file.");

            var plaintext = new byte[combined.Length - TagBytes];

            if (payload.Iterations > MaxPbkdf2Iters)
                throw new InvalidOperationException("Export file specifies an unsupported PBKDF2 iteration count.");

            // Clamp to the legacy count so a tampered payload can't force a weak derivation.
            var iterations = Math.Max(payload.Iterations ?? LegacyPbkdf2Iters, LegacyPbkdf2Iters);
            try
            {
                DecryptWithIterations(password, salt, nonce, combined, iterations, plaintext);
            }
            catch (AuthenticationTagMismatchException) when (payload.Iterations is null && iterations != Pbkdf2Iters)
            {
                // Files exported without the Iterations field may predate it (100k) or
                // come from an interim build that already used the current count.
                DecryptWithIterations(password, salt, nonce, combined, Pbkdf2Iters, plaintext);
            }

            // Content-sniff rather than trusting the (unauthenticated) Version field:
            // legacy exports decrypt to a JSON array, v2 bundles to an object.
            var firstToken = plaintext.FirstOrDefault(b => !char.IsWhiteSpace((char)b));
            if (firstToken == (byte)'{')
            {
                return JsonSerializer.Deserialize<HttpApiExportBundle>(plaintext, _json) ?? new HttpApiExportBundle();
            }

            var definitions = JsonSerializer.Deserialize<List<HttpApiDefinition>>(plaintext, _json) ?? [];
            return new HttpApiExportBundle { Definitions = definitions };
        }
        catch (AuthenticationTagMismatchException)
        {
            throw new InvalidOperationException("Incorrect password or corrupted file.");
        }
        catch (InvalidOperationException) { throw; }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Incorrect password or corrupted file.", ex);
        }
    }

    private static void DecryptWithIterations(string password, byte[] salt, byte[] nonce, byte[] combined, int iterations, byte[] plaintext)
    {
        var key       = DeriveKey(password, salt, iterations);
        var cipherLen = combined.Length - TagBytes;
        var cipher    = combined.AsSpan(0, cipherLen);
        var tag       = combined.AsSpan(cipherLen, TagBytes);

        using var aes = new AesGcm(key, TagBytes);
        aes.Decrypt(nonce, cipher, tag, plaintext);
    }

    private static byte[] DeriveKey(string password, ReadOnlySpan<byte> salt, int iterations = Pbkdf2Iters)
        => Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt.ToArray(),
            iterations,
            HashAlgorithmName.SHA256,
            KeyBytes);
}
