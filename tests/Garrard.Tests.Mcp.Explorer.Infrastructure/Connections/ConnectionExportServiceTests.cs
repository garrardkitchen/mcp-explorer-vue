using Garrard.Mcp.Explorer.Core.Domain.Connections;
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
}
