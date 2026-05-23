using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
    private const int Pbkdf2Iters = 100_000;

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(allowIntegerValues: true) }
    };

    public HttpApiExportPayload Encrypt(IReadOnlyList<HttpApiDefinition> definitions, string password)
    {
        var plaintext = JsonSerializer.SerializeToUtf8Bytes(definitions, _json);

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
            Salt  = Convert.ToBase64String(salt),
            Nonce = Convert.ToBase64String(nonce),
            Data  = Convert.ToBase64String(cipherBuf)
        };
    }

    public IReadOnlyList<HttpApiDefinition> Decrypt(HttpApiExportPayload payload, string password)
    {
        try
        {
            var salt     = Convert.FromBase64String(payload.Salt);
            var nonce    = Convert.FromBase64String(payload.Nonce);
            var combined = Convert.FromBase64String(payload.Data);

            if (combined.Length < TagBytes)
                throw new InvalidOperationException("Incorrect password or corrupted file.");

            var key       = DeriveKey(password, salt);
            var cipherLen = combined.Length - TagBytes;
            var cipher    = combined.AsSpan(0, cipherLen);
            var tag       = combined.AsSpan(cipherLen, TagBytes);
            var plaintext = new byte[cipherLen];

            using var aes = new AesGcm(key, TagBytes);
            aes.Decrypt(nonce, cipher, tag, plaintext);

            return JsonSerializer.Deserialize<List<HttpApiDefinition>>(plaintext, _json) ?? [];
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

    private static byte[] DeriveKey(string password, ReadOnlySpan<byte> salt)
        => Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt.ToArray(),
            Pbkdf2Iters,
            HashAlgorithmName.SHA256,
            KeyBytes);
}
