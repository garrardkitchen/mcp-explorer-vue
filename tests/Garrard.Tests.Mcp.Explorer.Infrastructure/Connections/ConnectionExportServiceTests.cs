using Garrard.Mcp.Explorer.Core.Domain.Connections;
using Garrard.Mcp.Explorer.Core.Interfaces;
using Garrard.Mcp.Explorer.Infrastructure.Connections;

namespace Garrard.Tests.Mcp.Explorer.Infrastructure.Connections;

/// <summary>Tests for <see cref="ConnectionExportService"/>.</summary>
public class ConnectionExportServiceTests
{
    private readonly ConnectionExportService _sut = new();

    private static ConnectionDefinition MakeConn(string name) => new()
    {
        Name     = name,
        Endpoint = $"http://{name}.example.com",
        Note     = $"Note for {name}"
    };

    // ── Encrypt → Decrypt round-trip ──────────────────────────────────────────

    [Fact]
    public void RoundTrip_SingleConnection_ReturnsOriginal()
    {
        var connections = new[] { MakeConn("my-server") };
        var payload = _sut.Encrypt(connections, "correct-password");
        var result  = _sut.Decrypt(payload, "correct-password");

        Assert.Single(result);
        Assert.Equal("my-server",                result[0].Name);
        Assert.Equal("http://my-server.example.com", result[0].Endpoint);
        Assert.Equal("Note for my-server",       result[0].Note);
    }

    [Fact]
    public void RoundTrip_MultipleConnections_PreservesAll()
    {
        var connections = new[] { MakeConn("alpha"), MakeConn("beta"), MakeConn("gamma") };
        var payload = _sut.Encrypt(connections, "s3cr3t!");
        var result  = _sut.Decrypt(payload, "s3cr3t!");

        Assert.Equal(3, result.Count);
        Assert.Equal("alpha", result[0].Name);
        Assert.Equal("beta",  result[1].Name);
        Assert.Equal("gamma", result[2].Name);
    }

    [Fact]
    public void RoundTrip_EmptyList_ReturnsEmpty()
    {
        var payload = _sut.Encrypt([], "pw");
        var result  = _sut.Decrypt(payload, "pw");
        Assert.Empty(result);
    }

    [Fact]
    public void RoundTrip_SpecialCharacterPassword_Succeeds()
    {
        var connections = new[] { MakeConn("conn") };
        const string pw = "P@$$w0rd!#£€%^&*()";
        var payload = _sut.Encrypt(connections, pw);
        var result  = _sut.Decrypt(payload, pw);
        Assert.Single(result);
    }

    // ── Wrong password ────────────────────────────────────────────────────────

    [Fact]
    public void Decrypt_WrongPassword_ThrowsInvalidOperationException()
    {
        var payload = _sut.Encrypt([MakeConn("conn")], "correct");
        Assert.Throws<InvalidOperationException>(() => _sut.Decrypt(payload, "wrong"));
    }

    [Fact]
    public void Decrypt_EmptyPassword_WhenEncryptedWithNonEmpty_Throws()
    {
        var payload = _sut.Encrypt([MakeConn("conn")], "secret");
        Assert.Throws<InvalidOperationException>(() => _sut.Decrypt(payload, ""));
    }

    // ── Payload uniqueness ────────────────────────────────────────────────────

    [Fact]
    public void Encrypt_SameInputTwice_ProducesDifferentPayloads()
    {
        var connections = new[] { MakeConn("conn") };
        var p1 = _sut.Encrypt(connections, "pw");
        var p2 = _sut.Encrypt(connections, "pw");

        // salt and nonce are random so both should differ
        Assert.NotEqual(p1.Salt,  p2.Salt);
        Assert.NotEqual(p1.Nonce, p2.Nonce);
        Assert.NotEqual(p1.Data,  p2.Data);
    }

    // ── Tamper detection ──────────────────────────────────────────────────────

    [Fact]
    public void Decrypt_TamperedData_Throws()
    {
        var payload = _sut.Encrypt([MakeConn("conn")], "pw");
        // Flip a byte in the ciphertext
        var dataBytes = Convert.FromBase64String(payload.Data);
        dataBytes[0] ^= 0xFF;
        var tampered = payload with { Data = Convert.ToBase64String(dataBytes) };

        Assert.Throws<InvalidOperationException>(() => _sut.Decrypt(tampered, "pw"));
    }

    // ── Credentials preserved ─────────────────────────────────────────────────

    [Fact]
    public void RoundTrip_PreservesAzureCredentials()
    {
        var conn = MakeConn("azure-conn");
        conn.AuthenticationMode = ConnectionAuthenticationMode.AzureClientCredentials;
        conn.AzureCredentials   = new() { TenantId = "t1", ClientId = "c1", ClientSecret = "s1", Scope = "scope" };

        var payload = _sut.Encrypt([conn], "pw");
        var result  = _sut.Decrypt(payload, "pw");

        Assert.Single(result);
        Assert.Equal("t1", result[0].AzureCredentials?.TenantId);
        Assert.Equal("c1", result[0].AzureCredentials?.ClientId);
        Assert.Equal("s1", result[0].AzureCredentials?.ClientSecret);
    }

    // ── PBKDF2 iteration compatibility ────────────────────────────────────────

    [Fact]
    public void Encrypt_EmbedsIterationCount()
    {
        var payload = _sut.Encrypt([MakeConn("conn")], "pw");

        Assert.NotNull(payload.Iterations);
        Assert.True(payload.Iterations >= 100_000);
    }

    [Fact]
    public void Decrypt_LegacyPayloadWithoutIterationsField_Succeeds()
    {
        // Files exported before the Iterations field existed were derived at 100,000 iterations.
        var payload = EncryptWithIterations([MakeConn("legacy-conn")], "old-password", 100_000);

        var result = _sut.Decrypt(payload, "old-password");

        Assert.Single(result);
        Assert.Equal("legacy-conn", result[0].Name);
    }

    [Fact]
    public void Decrypt_LowIterationsField_IsClampedToLegacyMinimum()
    {
        // A payload claiming a weak iteration count must still be derived at the 100k floor.
        var payload = EncryptWithIterations([MakeConn("conn")], "pw", 100_000) with { Iterations = 1_000 };

        var result = _sut.Decrypt(payload, "pw");

        Assert.Single(result);
    }

    [Fact]
    public void Decrypt_ExcessiveIterationsField_IsRejectedWithoutDeriving()
    {
        // A hostile payload claiming billions of iterations must be rejected up front,
        // not spent minutes of CPU deriving a key that can never match.
        var payload = EncryptWithIterations([MakeConn("conn")], "pw", 100_000) with { Iterations = int.MaxValue };

        var sw = System.Diagnostics.Stopwatch.StartNew();
        Assert.Throws<InvalidOperationException>(() => _sut.Decrypt(payload, "pw"));
        sw.Stop();

        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(2), "Rejection must happen before key derivation.");
    }

    private static ConnectionExportPayload EncryptWithIterations(
        IReadOnlyList<ConnectionDefinition> connections, string password, int iterations)
    {
        var json = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(connections, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        });

        var salt  = System.Security.Cryptography.RandomNumberGenerator.GetBytes(16);
        var nonce = System.Security.Cryptography.RandomNumberGenerator.GetBytes(12);
        var key   = System.Security.Cryptography.Rfc2898DeriveBytes.Pbkdf2(
            System.Text.Encoding.UTF8.GetBytes(password), salt, iterations,
            System.Security.Cryptography.HashAlgorithmName.SHA256, 32);

        var cipherBuf = new byte[json.Length + 16];
        using var aes = new System.Security.Cryptography.AesGcm(key, 16);
        aes.Encrypt(nonce, json, cipherBuf.AsSpan(0, json.Length), cipherBuf.AsSpan(json.Length, 16));

        return new ConnectionExportPayload
        {
            Salt  = Convert.ToBase64String(salt),
            Nonce = Convert.ToBase64String(nonce),
            Data  = Convert.ToBase64String(cipherBuf)
        };
    }
}
