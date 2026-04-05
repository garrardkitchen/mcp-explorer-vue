using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Garrard.Mcp.Explorer.Core.Domain.Connections;
using Garrard.Mcp.Explorer.Core.Interfaces;

namespace Garrard.Mcp.Explorer.Infrastructure.Connections;

/// <summary>
/// AES-256-GCM encryption using PBKDF2-SHA256 key derivation.
/// File format: JSON object with base64-encoded salt, nonce and ciphertext+tag.
/// </summary>
public sealed class ConnectionExportService : IConnectionExportService
{
    private const int KeyBytes        = 32;   // AES-256
    private const int SaltBytes       = 16;
    private const int NonceBytes      = 12;   // GCM standard nonce
    private const int TagBytes        = 16;   // GCM auth tag
    private const int Pbkdf2Iters     = 100_000;

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition      = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        WriteIndented               = false
    };

    public ConnectionExportPayload Encrypt(IReadOnlyList<ConnectionDefinition> connections, string password)
    {
        var plaintext = JsonSerializer.SerializeToUtf8Bytes(connections, _json);

        Span<byte> salt  = stackalloc byte[SaltBytes];
        Span<byte> nonce = stackalloc byte[NonceBytes];
        RandomNumberGenerator.Fill(salt);
        RandomNumberGenerator.Fill(nonce);

        var key       = DeriveKey(password, salt);
        var cipherLen = plaintext.Length + TagBytes;
        var cipherBuf = new byte[cipherLen];
        var cipher    = cipherBuf.AsSpan(0, plaintext.Length);
        var tag       = cipherBuf.AsSpan(plaintext.Length, TagBytes);

        using var aes = new AesGcm(key, TagBytes);
        aes.Encrypt(nonce, plaintext, cipher, tag);

        return new ConnectionExportPayload
        {
            Salt  = Convert.ToBase64String(salt),
            Nonce = Convert.ToBase64String(nonce),
            Data  = Convert.ToBase64String(cipherBuf)
        };
    }

    public IReadOnlyList<ConnectionDefinition> Decrypt(ConnectionExportPayload payload, string password)
    {
        try
        {
            var salt      = Convert.FromBase64String(payload.Salt);
            var nonce     = Convert.FromBase64String(payload.Nonce);
            var combined  = Convert.FromBase64String(payload.Data);

            if (combined.Length < TagBytes)
                throw new InvalidOperationException("Incorrect password or corrupted file.");

            var key          = DeriveKey(password, salt);
            var cipherLen    = combined.Length - TagBytes;
            var cipherText   = combined.AsSpan(0, cipherLen);
            var tag          = combined.AsSpan(cipherLen, TagBytes);
            var plaintext    = new byte[cipherLen];

            using var aes = new AesGcm(key, TagBytes);
            aes.Decrypt(nonce, cipherText, tag, plaintext);

            return JsonSerializer.Deserialize<List<ConnectionDefinition>>(plaintext, _json)
                   ?? [];
        }
        catch (AuthenticationTagMismatchException)
        {
            throw new InvalidOperationException("Incorrect password or corrupted file.");
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Incorrect password or corrupted file.", ex);
        }
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static byte[] DeriveKey(string password, ReadOnlySpan<byte> salt)
    {
        var saltArr = salt.ToArray();
        return Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            saltArr,
            Pbkdf2Iters,
            HashAlgorithmName.SHA256,
            KeyBytes);
    }
}
