using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Garrard.Mcp.Explorer.Core.Domain.Certificates;
using Garrard.Mcp.Explorer.Core.Domain.Connections;
using Garrard.Mcp.Explorer.Core.Domain.HttpApi;
using Garrard.Mcp.Explorer.Core.Domain.Preferences;
using Garrard.Mcp.Explorer.Core.Interfaces;
using Garrard.Mcp.Explorer.Infrastructure.Certificates;
using Moq;

namespace Garrard.Tests.Mcp.Explorer.Infrastructure.Certificates;

/// <summary>
/// Tests for <see cref="CertificateService"/>. Each test uses a unique temp data dir;
/// preference/HTTP API stores are mocked so usage tracking can be exercised without real files.
/// </summary>
public sealed class CertificateServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly Mock<IUserPreferencesStore> _prefsMock;
    private readonly Mock<IHttpApiStore> _httpApisMock;

    public CertificateServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "mcp-explorer-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);

        _prefsMock = new Mock<IUserPreferencesStore>();
        _prefsMock.Setup(p => p.LoadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new UserPreferences());

        _httpApisMock = new Mock<IHttpApiStore>();
        _httpApisMock.Setup(s => s.GetAllDefinitionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, recursive: true); }
        catch { /* best-effort cleanup */ }
    }

    private CertificateService CreateService()
        => new(Path.Combine(_testDir, "settings.json"), _prefsMock.Object, _httpApisMock.Object);

    private static GenerateCertificateRequest Request(string name = "test-cert", int keySize = 2048, int months = 12, string? pfxPassword = null)
        => new() { Name = name, KeySize = keySize, ValidityMonths = months, PfxPassword = pfxPassword };

    // ── Generation ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GenerateSelfSigned_WritesExpectedFilesAndMetadata()
    {
        var service = CreateService();

        var result = await service.GenerateSelfSignedAsync(Request());

        Assert.True(result.Success);
        Assert.All(result.Steps, s => Assert.Equal(StepStatus.Succeeded, s.Status));
        Assert.NotNull(result.Certificate);

        var certDir = Path.Combine(service.CertificatesDirectory, "test-cert");
        Assert.True(File.Exists(Path.Combine(certDir, "cert.pem")));
        Assert.True(File.Exists(Path.Combine(certDir, "key.pem")));
        Assert.True(File.Exists(Path.Combine(certDir, "metadata.json")));
        Assert.False(File.Exists(Path.Combine(certDir, "cert.pfx"))); // no password given
        Assert.True(File.Exists(Path.Combine(service.CertificatesDirectory, ".gitignore")));

        var info = result.Certificate!;
        Assert.Equal("CN=mcp-explorer-test-cert", info.Subject);
        Assert.Equal(2048, info.KeySize);
        Assert.Equal(CertificateSource.SelfSigned, info.Source);
        Assert.Equal(CertificateState.Active, info.State);
        Assert.False(string.IsNullOrEmpty(info.ThumbprintSha1));
        Assert.False(string.IsNullOrEmpty(info.ThumbprintSha256));
        Assert.NotNull(info.NotAfter);
        // ~12 months validity
        Assert.InRange(info.NotAfter!.Value, DateTimeOffset.UtcNow.AddMonths(11), DateTimeOffset.UtcNow.AddMonths(13));
    }

    [Fact]
    public async Task GenerateSelfSigned_ThumbprintsMatchTheWrittenCertificate()
    {
        var service = CreateService();
        var result = await service.GenerateSelfSignedAsync(Request());

        var certPath = Path.Combine(service.CertificatesDirectory, "test-cert", "cert.pem");
        using var parsed = X509Certificate2.CreateFromPem(await File.ReadAllTextAsync(certPath));

        Assert.Equal(parsed.Thumbprint, result.Certificate!.ThumbprintSha1);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(parsed.RawData)), result.Certificate.ThumbprintSha256);
    }

    [Fact]
    public async Task GenerateSelfSigned_PrivateKeyIsOwnerOnly_OnUnix()
    {
        if (OperatingSystem.IsWindows()) return;

        var service = CreateService();
        await service.GenerateSelfSignedAsync(Request());

        var keyPath = Path.Combine(service.CertificatesDirectory, "test-cert", "key.pem");
        var mode = File.GetUnixFileMode(keyPath);
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, mode);
    }

    [Fact]
    public async Task GenerateSelfSigned_WithPfxPassword_WritesProtectedPfx()
    {
        var service = CreateService();

        var result = await service.GenerateSelfSignedAsync(Request(pfxPassword: "s3cret!"));

        Assert.True(result.Success);
        Assert.True(result.Certificate!.HasPfx);
        var pfxPath = Path.Combine(service.CertificatesDirectory, "test-cert", "cert.pfx");
        Assert.True(File.Exists(pfxPath));

        // Round-trip: the PFX opens with the password and has a private key
        var loaded = X509CertificateLoader.LoadPkcs12(await File.ReadAllBytesAsync(pfxPath), "s3cret!");
        Assert.True(loaded.HasPrivateKey);
    }

    [Theory]
    [InlineData("../evil")]
    [InlineData("UPPERCASE")]
    [InlineData("has space")]
    [InlineData("-leading")]
    [InlineData("trailing-")]
    [InlineData("")]
    public async Task GenerateSelfSigned_InvalidName_FailsValidationStep(string name)
    {
        var service = CreateService();

        var result = await service.GenerateSelfSignedAsync(Request(name: name));

        Assert.False(result.Success);
        Assert.Equal(StepStatus.Failed, result.Steps.First(s => s.Id == "validate").Status);
        Assert.False(Directory.Exists(Path.Combine(service.CertificatesDirectory, name)));
    }

    [Fact]
    public async Task GenerateSelfSigned_DuplicateName_Fails()
    {
        var service = CreateService();
        await service.GenerateSelfSignedAsync(Request());

        var result = await service.GenerateSelfSignedAsync(Request());

        Assert.False(result.Success);
        Assert.Contains("already exists", result.Steps.First(s => s.Id == "validate").Message);
    }

    [Fact]
    public async Task GenerateSelfSigned_InvalidKeySize_Fails()
    {
        var service = CreateService();

        var result = await service.GenerateSelfSignedAsync(Request(keySize: 1024));

        Assert.False(result.Success);
    }

    // ── Listing / expiring ────────────────────────────────────────────────────

    [Fact]
    public async Task ListAsync_ReturnsGeneratedCertificates_SortedByExpiry()
    {
        var service = CreateService();
        await service.GenerateSelfSignedAsync(Request("cert-long", months: 24));
        await service.GenerateSelfSignedAsync(Request("cert-short", months: 6));

        var list = await service.ListAsync();

        Assert.Equal(2, list.Count);
        Assert.Equal("cert-short", list[0].Name); // soonest expiry first
    }

    [Fact]
    public async Task GetExpiringAsync_ExcludesCertsBeyondWindow()
    {
        var service = CreateService();
        await service.GenerateSelfSignedAsync(Request("cert-ok", months: 12));

        var expiring = await service.GetExpiringAsync(days: 30);

        Assert.Empty(expiring);
    }

    // ── PFX export / public PEM ───────────────────────────────────────────────

    [Fact]
    public async Task ExportPfxAsync_RequiresPassword()
    {
        var service = CreateService();
        await service.GenerateSelfSignedAsync(Request());

        await Assert.ThrowsAsync<ArgumentException>(() => service.ExportPfxAsync("test-cert", " "));
    }

    [Fact]
    public async Task ExportPfxAsync_RoundTripsWithPassword()
    {
        var service = CreateService();
        await service.GenerateSelfSignedAsync(Request());

        var bytes = await service.ExportPfxAsync("test-cert", "pfx-pass");

        var loaded = X509CertificateLoader.LoadPkcs12(bytes, "pfx-pass");
        Assert.True(loaded.HasPrivateKey);
    }

    [Fact]
    public async Task GetPublicCertPemAsync_ContainsNoPrivateKey()
    {
        var service = CreateService();
        await service.GenerateSelfSignedAsync(Request());

        var pem = await service.GetPublicCertPemAsync("test-cert");

        Assert.Contains("BEGIN CERTIFICATE", pem);
        Assert.DoesNotContain("PRIVATE KEY", pem);
    }

    [Fact]
    public async Task LoadWithPrivateKeyAsync_ReturnsUsableCertificate()
    {
        var service = CreateService();
        await service.GenerateSelfSignedAsync(Request());

        using var cert = await service.LoadWithPrivateKeyAsync("test-cert");

        Assert.True(cert.HasPrivateKey);
    }

    // ── Usage tracking / delete ───────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_Blocked_WhenReferencedByConnection()
    {
        _prefsMock.Setup(p => p.LoadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new UserPreferences
        {
            Connections =
            [
                new ConnectionDefinition
                {
                    Name = "finance-mcp",
                    AzureCredentials = new AzureClientCredentialsOptions
                    {
                        CertificateRef = new CertificateReference { CertificateName = "test-cert" }
                    }
                }
            ]
        });

        var service = CreateService();
        await service.GenerateSelfSignedAsync(Request());

        var ex = await Assert.ThrowsAsync<CertificateInUseException>(() => service.DeleteAsync("test-cert"));
        Assert.Contains("finance-mcp", ex.Usage.ConnectionNames);
        Assert.True(Directory.Exists(Path.Combine(service.CertificatesDirectory, "test-cert")));
    }

    [Fact]
    public async Task DeleteAsync_Blocked_WhenReferencedByHttpApi()
    {
        _httpApisMock.Setup(s => s.GetAllDefinitionsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new HttpApiDefinition
            {
                Name = "finance-http",
                AzureCredentials = new HttpApiAzureCredentialsOptions
                {
                    CertificateRef = new CertificateReference { CertificateName = "test-cert" }
                }
            }
        ]);

        var service = CreateService();
        await service.GenerateSelfSignedAsync(Request());

        var ex = await Assert.ThrowsAsync<CertificateInUseException>(() => service.DeleteAsync("test-cert"));
        Assert.Contains("finance-http", ex.Usage.HttpApiNames);
    }

    [Fact]
    public async Task DeleteAsync_RemovesUnreferencedCertificate()
    {
        var service = CreateService();
        await service.GenerateSelfSignedAsync(Request());

        await service.DeleteAsync("test-cert");

        Assert.False(Directory.Exists(Path.Combine(service.CertificatesDirectory, "test-cert")));
        Assert.Null(await service.GetAsync("test-cert"));
    }

    // ── Upload records ────────────────────────────────────────────────────────

    [Fact]
    public async Task RecordUploadAsync_PersistsAndReplacesPerApp()
    {
        var service = CreateService();
        await service.GenerateSelfSignedAsync(Request());

        await service.RecordUploadAsync("test-cert", new CertificateUploadRecord
        {
            AppObjectId = "obj-1", AppId = "app-1", DisplayName = "finance-api-client",
            KeyId = "key-1", UploadedThumbprintSha1 = "AAA", UploadedAt = DateTimeOffset.UtcNow
        });
        await service.RecordUploadAsync("test-cert", new CertificateUploadRecord
        {
            AppObjectId = "obj-1", AppId = "app-1", DisplayName = "finance-api-client",
            KeyId = "key-2", UploadedThumbprintSha1 = "BBB", UploadedAt = DateTimeOffset.UtcNow
        });

        var info = await service.GetAsync("test-cert");
        var record = Assert.Single(info!.UploadedTo); // same app replaced, not appended
        Assert.Equal("key-2", record.KeyId);

        await service.RemoveUploadRecordAsync("test-cert", "key-2");
        info = await service.GetAsync("test-cert");
        Assert.Empty(info!.UploadedTo);
    }

    // ── CSR flow ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task CsrFlow_CreateDownloadImport_Works()
    {
        var service = CreateService();

        var csrResult = await service.CreateCsrAsync(new CreateCsrRequest { Name = "csr-cert" });
        Assert.True(csrResult.Success);
        Assert.Equal(CertificateState.CsrPending, csrResult.Certificate!.State);

        var csrPem = await service.GetCsrPemAsync("csr-cert");
        Assert.Contains("BEGIN CERTIFICATE REQUEST", csrPem);

        // "Issue" the cert ourselves: sign a certificate for the CSR's key
        var keyPem = await File.ReadAllTextAsync(Path.Combine(service.CertificatesDirectory, "csr-cert", "key.pem"));
        using var rsa = RSA.Create();
        rsa.ImportFromPem(keyPem);
        var request = new CertificateRequest("CN=mcp-explorer-csr-cert", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var issued = request.CreateSelfSigned(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(1));
        var issuedPem = new string(System.Security.Cryptography.PemEncoding.Write("CERTIFICATE", issued.RawData));

        var importResult = await service.ImportIssuedCertificateAsync("csr-cert", issuedPem);

        Assert.True(importResult.Success);
        Assert.Equal(CertificateState.Active, importResult.Certificate!.State);
        Assert.Equal(issued.Thumbprint, importResult.Certificate.ThumbprintSha1);
    }

    [Fact]
    public async Task ImportIssuedCertificate_RejectsMismatchedKey()
    {
        var service = CreateService();
        await service.CreateCsrAsync(new CreateCsrRequest { Name = "csr-cert" });

        // Certificate for a *different* key
        using var otherRsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=wrong", otherRsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var issued = request.CreateSelfSigned(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(1));
        var issuedPem = new string(System.Security.Cryptography.PemEncoding.Write("CERTIFICATE", issued.RawData));

        var result = await service.ImportIssuedCertificateAsync("csr-cert", issuedPem);

        Assert.False(result.Success);
        Assert.Equal(StepStatus.Failed, result.Steps.First(s => s.Id == "verify-key").Status);
    }

    // ── Import ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ImportAsync_RoundTripsAGeneratedCertificate()
    {
        var service = CreateService();
        await service.GenerateSelfSignedAsync(Request("original"));

        var certPem = await service.GetPublicCertPemAsync("original");
        var keyPem = await File.ReadAllTextAsync(Path.Combine(service.CertificatesDirectory, "original", "key.pem"));

        var imported = await service.ImportAsync("copy", certPem, keyPem, null, CertificateSource.SelfSigned);

        Assert.Equal(CertificateState.Active, imported.State);
        var original = await service.GetAsync("original");
        Assert.Equal(original!.ThumbprintSha1, imported.ThumbprintSha1);

        if (!OperatingSystem.IsWindows())
        {
            var mode = File.GetUnixFileMode(Path.Combine(service.CertificatesDirectory, "copy", "key.pem"));
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, mode);
        }
    }

    // ── Audit ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AuditLog_RecordsOperations()
    {
        var service = CreateService();
        await service.GenerateSelfSignedAsync(Request());
        await service.ExportPfxAsync("test-cert", "pw");
        await service.DeleteAsync("test-cert");

        var entries = await service.ReadAuditAsync();

        Assert.Contains(entries, e => e.Action == "create" && e.CertificateName == "test-cert");
        Assert.Contains(entries, e => e.Action == "export-pfx");
        Assert.Contains(entries, e => e.Action == "delete");

        var filtered = await service.ReadAuditAsync("test-cert");
        Assert.Equal(3, filtered.Count);
    }
}
