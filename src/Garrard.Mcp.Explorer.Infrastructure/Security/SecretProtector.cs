using System.Security.Cryptography;
using System.Text;
using Garrard.Mcp.Explorer.Core.Interfaces;

namespace Garrard.Mcp.Explorer.Infrastructure.Security;

/// <summary>
/// AES-256 based secret protector. On Windows the key file is additionally wrapped with DPAPI.
/// Falls back to a deterministic machine-derived key when file I/O fails.
/// </summary>
/// <remarks>
/// Pass <paramref name="keyDirectory"/> to override the default key location
/// (useful when running in a container with a mounted data volume).
/// </remarks>
public sealed class SecretProtector : ISecretProtector
{
    private const string Prefix = "enc:";
    private const string KeyFileName = "secret.key";
    private const byte PlainKeyMarker = 0x01;
    private const byte ProtectedKeyMarker = 0x02;

    private readonly Lazy<byte[]> _key;

    public SecretProtector(string? keyDirectory = null)
    {
        // Capture keyDirectory for use inside the lazy initializer
        var resolvedDir = string.IsNullOrWhiteSpace(keyDirectory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "McpExplorer")
            : keyDirectory;
        _key = new Lazy<byte[]>(() => GetOrCreateKey(resolvedDir), LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public string Encrypt(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext)) return plaintext;
        if (plaintext.StartsWith(Prefix, StringComparison.Ordinal)) return plaintext;

        try
        {
            using var aes = Aes.Create();
            aes.Key = _key.Value;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.GenerateIV();

            var iv = aes.IV;
            using var encryptor = aes.CreateEncryptor(aes.Key, iv);
            var bytes = Encoding.UTF8.GetBytes(plaintext);
            var cipher = encryptor.TransformFinalBlock(bytes, 0, bytes.Length);

            var combined = new byte[iv.Length + cipher.Length];
            Buffer.BlockCopy(iv, 0, combined, 0, iv.Length);
            Buffer.BlockCopy(cipher, 0, combined, iv.Length, cipher.Length);

            return Prefix + Convert.ToBase64String(combined);
        }
        catch
        {
            return plaintext;
        }
    }

    public string Decrypt(string ciphertext)
    {
        if (string.IsNullOrEmpty(ciphertext)) return ciphertext;
        if (!ciphertext.StartsWith(Prefix, StringComparison.Ordinal)) return ciphertext;

        try
        {
            var data = Convert.FromBase64String(ciphertext[Prefix.Length..]);
            using var aes = Aes.Create();
            aes.Key = _key.Value;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            var ivSize = aes.BlockSize / 8;
            if (data.Length < ivSize + 1) return ciphertext;

            var iv = new byte[ivSize];
            var cipher = new byte[data.Length - ivSize];
            Buffer.BlockCopy(data, 0, iv, 0, ivSize);
            Buffer.BlockCopy(data, ivSize, cipher, 0, cipher.Length);

            using var decryptor = aes.CreateDecryptor(aes.Key, iv);
            var plainBytes = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch
        {
            var legacy = TryLegacyDecrypt(ciphertext);
            return legacy ?? ciphertext;
        }
    }

    private static byte[] GetOrCreateKey(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            var keyPath = Path.Combine(directory, KeyFileName);

            if (File.Exists(keyPath))
            {
                var persisted = File.ReadAllBytes(keyPath);
                return UnwrapKey(persisted);
            }

            var key = RandomNumberGenerator.GetBytes(32);
            var wrapped = WrapKey(key);
            File.WriteAllBytes(keyPath, wrapped);
            TryHardenPermissions(keyPath);
            return key;
        }
        catch
        {
            return DeriveLegacyKey();
        }
    }

    private static byte[] WrapKey(byte[] key)
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                var protectedKey = ProtectedData.Protect(key, null, DataProtectionScope.CurrentUser);
                return Combine(ProtectedKeyMarker, protectedKey);
            }
            catch { }
        }
        return Combine(PlainKeyMarker, key);
    }

    private static byte[] UnwrapKey(byte[] persisted)
    {
        if (persisted.Length <= 1)
            throw new CryptographicException("Secret key payload is invalid.");

        var marker = persisted[0];
        var payload = new byte[persisted.Length - 1];
        Buffer.BlockCopy(persisted, 1, payload, 0, payload.Length);

        if (marker == ProtectedKeyMarker && OperatingSystem.IsWindows())
            return ProtectedData.Unprotect(payload, null, DataProtectionScope.CurrentUser);

        if (marker == PlainKeyMarker)
            return payload;

        throw new CryptographicException("Secret key payload format is not supported on this platform.");
    }

    private static byte[] Combine(byte marker, byte[] payload)
    {
        var combined = new byte[payload.Length + 1];
        combined[0] = marker;
        Buffer.BlockCopy(payload, 0, combined, 1, payload.Length);
        return combined;
    }

    private static void TryHardenPermissions(string keyPath)
    {
        try
        {
            if (OperatingSystem.IsWindows())
                File.SetAttributes(keyPath, FileAttributes.Hidden | FileAttributes.NotContentIndexed);
            else if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
                File.SetUnixFileMode(keyPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch { }
    }

    private static string? TryLegacyDecrypt(string cipherText)
    {
        try
        {
            var data = Convert.FromBase64String(cipherText[Prefix.Length..]);
            using var aes = Aes.Create();
            aes.Key = DeriveLegacyKey();
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            var ivSize = aes.BlockSize / 8;
            if (data.Length < ivSize + 1) return null;

            var iv = new byte[ivSize];
            var cipher = new byte[data.Length - ivSize];
            Buffer.BlockCopy(data, 0, iv, 0, ivSize);
            Buffer.BlockCopy(data, ivSize, cipher, 0, cipher.Length);

            using var legacyDecryptor = aes.CreateDecryptor(aes.Key, iv);
            var plainBytes = legacyDecryptor.TransformFinalBlock(cipher, 0, cipher.Length);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch
        {
            return null;
        }
    }

    private static byte[] DeriveLegacyKey()
    {
        var id = $"{Environment.MachineName}|{Environment.UserDomainName}|{Environment.UserName}|McpExplorer-v1";
        using var sha = SHA256.Create();
        return sha.ComputeHash(Encoding.UTF8.GetBytes(id));
    }
}

