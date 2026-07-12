using Garrard.Mcp.Explorer.Core.Domain.Certificates;
using Garrard.Mcp.Explorer.Core.Domain.Connections;
using Garrard.Mcp.Explorer.Core.Domain.HttpApi;
using Garrard.Mcp.Explorer.Core.Domain.Preferences;
using Garrard.Mcp.Explorer.Core.Interfaces;
using Garrard.Mcp.Explorer.Infrastructure.Certificates;
using Garrard.Mcp.Explorer.Infrastructure.Connections;
using Garrard.Mcp.Explorer.Infrastructure.HttpApi;
using Moq;

namespace Garrard.Tests.Mcp.Explorer.Infrastructure.Certificates;

/// <summary>
/// Export/import bundles carrying certificates: round-trips, legacy compatibility,
/// wrong password, and re-import into a certificate store on disk (0600 restored).
/// </summary>
public sealed class CertificateExportBundleTests : IDisposable
{
    private readonly string _testDir;
    private readonly CertificateService _certService;
    private readonly ConnectionExportService _connectionExport = new();
    private readonly HttpApiExportService _httpApiExport = new();

    public CertificateExportBundleTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "mcp-explorer-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);

        var prefsMock = new Mock<IUserPreferencesStore>();
        prefsMock.Setup(p => p.LoadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new UserPreferences());
        var httpMock = new Mock<IHttpApiStore>();
        httpMock.Setup(s => s.GetAllDefinitionsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        _certService = new CertificateService(Path.Combine(_testDir, "settings.json"), prefsMock.Object, httpMock.Object);
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, recursive: true); }
        catch { /* best-effort cleanup */ }
    }

    private async Task<ExportedCertificate> CreateAndExportCertAsync(string name = "bundle-cert")
    {
        var generated = await _certService.GenerateSelfSignedAsync(new GenerateCertificateRequest { Name = name });
        Assert.True(generated.Success);
        return await _certService.ExportForBundleAsync(name);
    }

    [Fact]
    public async Task ConnectionBundle_WithCertificates_RoundTrips()
    {
        var cert = await CreateAndExportCertAsync();
        var connections = new[]
        {
            new ConnectionDefinition
            {
                Name = "conn-1",
                AzureCredentials = new AzureClientCredentialsOptions
                {
                    CertificateRef = new CertificateReference { CertificateName = "bundle-cert" },
                },
            },
        };

        var payload = _connectionExport.Encrypt(connections, [cert], "pw");
        Assert.Equal(2, payload.Version);

        var bundle = _connectionExport.DecryptBundle(payload, "pw");

        Assert.Single(bundle.Connections);
        Assert.Equal("bundle-cert", bundle.Connections[0].AzureCredentials!.CertificateRef!.CertificateName);
        var exported = Assert.Single(bundle.Certificates);
        Assert.Equal(cert.CertPem, exported.CertPem);
        Assert.Equal(cert.KeyPem, exported.KeyPem);
        Assert.Contains("PRIVATE KEY", exported.KeyPem);
    }

    [Fact]
    public async Task ConnectionBundle_WithoutCertificates_KeepsLegacyArrayFormat()
    {
        var connections = new[] { new ConnectionDefinition { Name = "conn-1" } };
        var payload = _connectionExport.Encrypt(connections, [], "pw");

        Assert.Equal(1, payload.Version);
        // Legacy Decrypt path (as used by older builds) still works
        var legacy = _connectionExport.Decrypt(payload, "pw");
        Assert.Single(legacy);

        await Task.CompletedTask;
    }

    [Fact]
    public async Task ConnectionBundle_WrongPassword_FailsCleanly()
    {
        var cert = await CreateAndExportCertAsync();
        var payload = _connectionExport.Encrypt([new ConnectionDefinition { Name = "c" }], [cert], "right");

        var ex = Assert.Throws<InvalidOperationException>(() => _connectionExport.DecryptBundle(payload, "wrong"));
        Assert.Contains("Incorrect password", ex.Message);
    }

    [Fact]
    public async Task HttpApiBundle_WithCertificates_RoundTrips()
    {
        var cert = await CreateAndExportCertAsync();
        var definitions = new[] { new HttpApiDefinition { Id = "1", Name = "api-1" } };

        var payload = _httpApiExport.Encrypt(definitions, [cert], "pw");
        var bundle = _httpApiExport.DecryptBundle(payload, "pw");

        Assert.Single(bundle.Definitions);
        Assert.Single(bundle.Certificates);
    }

    [Fact]
    public async Task ImportedBundleCertificate_RestoresKeyWithOwnerOnlyMode()
    {
        var cert = await CreateAndExportCertAsync();

        // Import into a *fresh* store (simulates a clean data dir on another machine)
        var otherDir = Path.Combine(_testDir, "other");
        Directory.CreateDirectory(otherDir);
        var prefsMock = new Mock<IUserPreferencesStore>();
        prefsMock.Setup(p => p.LoadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new UserPreferences());
        var httpMock = new Mock<IHttpApiStore>();
        httpMock.Setup(s => s.GetAllDefinitionsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var freshStore = new CertificateService(Path.Combine(otherDir, "settings.json"), prefsMock.Object, httpMock.Object);

        var imported = await freshStore.ImportAsync(cert.Name, cert.CertPem, cert.KeyPem, null, cert.Source);

        Assert.Equal(CertificateState.Active, imported.State);
        var original = await _certService.GetAsync("bundle-cert");
        Assert.Equal(original!.ThumbprintSha1, imported.ThumbprintSha1);

        if (!OperatingSystem.IsWindows())
        {
            var mode = File.GetUnixFileMode(Path.Combine(freshStore.CertificatesDirectory, cert.Name, "key.pem"));
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, mode);
        }
    }
}
