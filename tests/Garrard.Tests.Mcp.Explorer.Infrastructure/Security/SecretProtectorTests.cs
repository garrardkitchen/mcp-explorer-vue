using Garrard.Mcp.Explorer.Infrastructure.Security;

namespace Garrard.Tests.Mcp.Explorer.Infrastructure.Security;

/// <summary>
/// Tests for <see cref="SecretProtector"/>.
/// The implementation uses a lazily-initialised AES-256 key stored on disk;
/// no mocking is required — the protector is exercised as a real object.
/// </summary>
public class SecretProtectorTests
{
    private readonly SecretProtector _sut = new();

    // ── Encrypt → Decrypt round-trip ──────────────────────────────────────────

    [Fact]
    public void RoundTrip_PlainValue_ReturnsSameString()
    {
        const string original = "super-secret-api-key-12345";

        var encrypted = _sut.Encrypt(original);
        var decrypted = _sut.Decrypt(encrypted);

        Assert.Equal(original, decrypted);
    }

    [Theory]
    [InlineData("short")]
    [InlineData("a longer passphrase with spaces")]
    [InlineData("unicode: 日本語テスト")]
    [InlineData("special chars: !@#$%^&*(){}[]")]
    public void RoundTrip_VariousPlainValues_ReturnOriginal(string value)
    {
        var encrypted = _sut.Encrypt(value);
        var decrypted = _sut.Decrypt(encrypted);

        Assert.Equal(value, decrypted);
    }

    // ── Encrypted value differs from plaintext ────────────────────────────────

    [Fact]
    public void Encrypt_Result_DiffersFromPlaintext()
    {
        const string value = "my-api-key";

        var encrypted = _sut.Encrypt(value);

        Assert.NotEqual(value, encrypted);
    }

    [Fact]
    public void Encrypt_Result_StartsWithEncPrefix()
    {
        var encrypted = _sut.Encrypt("any-value");

        Assert.StartsWith("enc:", encrypted);
    }

    // ── Empty string ──────────────────────────────────────────────────────────

    [Fact]
    public void Encrypt_EmptyString_ReturnsEmptyString()
    {
        var result = _sut.Encrypt(string.Empty);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Decrypt_EmptyString_ReturnsEmptyString()
    {
        var result = _sut.Decrypt(string.Empty);

        Assert.Equal(string.Empty, result);
    }

    // ── Already-encrypted values are idempotent ───────────────────────────────

    [Fact]
    public void Encrypt_AlreadyEncryptedValue_ReturnsUnchanged()
    {
        const string value = "enc:alreadyencoded==";

        var result = _sut.Encrypt(value);

        Assert.Equal(value, result);
    }

    // ── Random IV means same input → different ciphertext ────────────────────

    [Fact]
    public void Encrypt_SameValueTwice_ProducesDifferentCiphertext()
    {
        const string value = "my-secret";

        var cipher1 = _sut.Encrypt(value);
        var cipher2 = _sut.Encrypt(value);

        Assert.NotEqual(cipher1, cipher2);
    }

    // ── Decrypt of corrupt data returns ciphertext unchanged ──────────────────

    [Fact]
    public void Decrypt_CorruptBase64AfterPrefix_ReturnsOriginalCiphertext()
    {
        // "enc:" prefix present but the payload is not valid base-64
        const string corrupt = "enc:!!!not-valid-base64!!!";

        var result = _sut.Decrypt(corrupt);

        // Implementation swallows the error and returns the original string
        Assert.Equal(corrupt, result);
    }

    [Fact]
    public void Decrypt_ValidPrefixButTruncatedPayload_ReturnsOriginalCiphertext()
    {
        // Valid base-64 but too short to contain a full IV (16 bytes) + ciphertext
        const string truncated = "enc:AAEC"; // only 3 bytes decoded

        var result = _sut.Decrypt(truncated);

        Assert.Equal(truncated, result);
    }

    // ── Non-prefixed string passes through decrypt unchanged ──────────────────

    [Fact]
    public void Decrypt_PlainString_ReturnsUnchanged()
    {
        const string plain = "not-yet-encrypted";

        var result = _sut.Decrypt(plain);

        Assert.Equal(plain, result);
    }
}
