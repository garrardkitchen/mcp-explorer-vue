using Garrard.Mcp.Explorer.Core.Domain.Certificates;
using Garrard.Mcp.Explorer.Core.Domain.Connections;
using Garrard.Mcp.Explorer.Core.Domain.HttpApi;
using Garrard.Mcp.Explorer.Core.Domain.Preferences;
using Garrard.Mcp.Explorer.Core.Interfaces;
using Garrard.Mcp.Explorer.Infrastructure.Certificates;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Garrard.Tests.Mcp.Explorer.Infrastructure.Certificates;

public sealed class CertificateRenewalServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly Mock<IUserPreferencesStore> _prefsMock;
    private readonly Mock<IHttpApiStore> _httpApisMock;
    private readonly Mock<ICertificateUploadService> _uploadMock;
    private readonly CertificateService _certService;

    private UserPreferences _prefs = new();
    private readonly List<HttpApiDefinition> _httpDefs = [];

    public CertificateRenewalServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "mcp-explorer-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);

        _prefsMock = new Mock<IUserPreferencesStore>();
        _prefsMock.Setup(p => p.LoadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => _prefs);
        _prefsMock.Setup(p => p.SaveAsync(It.IsAny<UserPreferences>(), It.IsAny<CancellationToken>()))
            .Callback<UserPreferences, CancellationToken>((p, _) => _prefs = p)
            .Returns(Task.CompletedTask);

        _httpApisMock = new Mock<IHttpApiStore>();
        _httpApisMock.Setup(s => s.GetAllDefinitionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _httpDefs.ToList());
        _httpApisMock.Setup(s => s.SaveDefinitionAsync(It.IsAny<HttpApiDefinition>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((HttpApiDefinition d, CancellationToken _) => d);

        _uploadMock = new Mock<ICertificateUploadService>();

        _certService = new CertificateService(
            Path.Combine(_testDir, "settings.json"), _prefsMock.Object, _httpApisMock.Object);
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, recursive: true); }
        catch { /* best-effort cleanup */ }
    }

    private CertificateRenewalService CreateService() => new(
        _certService, _uploadMock.Object, _prefsMock.Object, _httpApisMock.Object,
        NullLogger<CertificateRenewalService>.Instance);

    private async Task<CertificateInfo> CreateCertAsync(string name = "api-cert")
    {
        var result = await _certService.GenerateSelfSignedAsync(new GenerateCertificateRequest { Name = name });
        Assert.True(result.Success);
        return result.Certificate!;
    }

    private void ReferenceFromConnection(string certName, string connectionName = "finance-mcp")
    {
        _prefs = _prefs with
        {
            Connections =
            [
                .._prefs.Connections,
                new ConnectionDefinition
                {
                    Name = connectionName,
                    AuthenticationMode = ConnectionAuthenticationMode.AzureClientCredentials,
                    AzureCredentials = new AzureClientCredentialsOptions
                    {
                        TenantId = "t", ClientId = "c", Scope = "s",
                        CertificateRef = new CertificateReference { CertificateName = certName },
                    },
                }
            ]
        };
    }

    [Fact]
    public async Task Renew_GeneratesSuccessor_RepointsReferences_SupersedesOld()
    {
        await CreateCertAsync();
        ReferenceFromConnection("api-cert");
        _httpDefs.Add(new HttpApiDefinition
        {
            Name = "finance-http",
            AzureCredentials = new HttpApiAzureCredentialsOptions
            {
                CertificateRef = new CertificateReference { CertificateName = "api-cert" },
            },
        });

        var result = await CreateService().RenewAsync("api-cert", removeOldKeyCredential: false);

        Assert.True(result.Success);
        Assert.Equal("api-cert-r2", result.Certificate!.Name);

        // Old cert superseded, successor active
        var old = await _certService.GetAsync("api-cert");
        Assert.Equal(CertificateState.Superseded, old!.State);
        Assert.Equal("api-cert-r2", old.RenewedBy);

        // References repointed
        Assert.Equal("api-cert-r2", _prefs.Connections[0].AzureCredentials!.CertificateRef!.CertificateName);
        Assert.Equal("api-cert-r2", _httpDefs[0].AzureCredentials!.CertificateRef!.CertificateName);

        // Upload skipped — the old cert was never uploaded
        Assert.Equal(StepStatus.Skipped, result.Steps.First(s => s.Id == "upload").Status);
        _uploadMock.Verify(u => u.UploadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Renew_UploadsSuccessorToEveryRecordedApp_AndOptionallyCleansUp()
    {
        await CreateCertAsync();
        await _certService.RecordUploadAsync("api-cert", new CertificateUploadRecord
        {
            AppObjectId = "obj-1", AppId = "app-1", DisplayName = "client-1",
            KeyId = "key-1", UploadedThumbprintSha1 = "AAA", UploadedAt = DateTimeOffset.UtcNow,
        });

        _uploadMock.Setup(u => u.UploadAsync("api-cert-r2", "app-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult.Succeeded([]));

        var result = await CreateService().RenewAsync("api-cert", removeOldKeyCredential: true);

        Assert.True(result.Success);
        _uploadMock.Verify(u => u.UploadAsync("api-cert-r2", "app-1", It.IsAny<CancellationToken>()), Times.Once);
        _uploadMock.Verify(u => u.RemoveKeyCredentialAsync("app-1", "key-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Renew_UploadFailure_LeavesOldCertAndReferencesUntouched()
    {
        await CreateCertAsync();
        ReferenceFromConnection("api-cert");
        await _certService.RecordUploadAsync("api-cert", new CertificateUploadRecord
        {
            AppObjectId = "obj-1", AppId = "app-1", DisplayName = "client-1",
            KeyId = "key-1", UploadedThumbprintSha1 = "AAA", UploadedAt = DateTimeOffset.UtcNow,
        });

        _uploadMock.Setup(u => u.UploadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult.Failed([new StepResult("fetch-app", "Fetch", StepStatus.Failed, "403")]));

        var result = await CreateService().RenewAsync("api-cert", removeOldKeyCredential: true);

        Assert.False(result.Success);
        Assert.Equal(StepStatus.Failed, result.Steps.First(s => s.Id == "upload").Status);

        // Old cert still active and still referenced
        var old = await _certService.GetAsync("api-cert");
        Assert.Equal(CertificateState.Active, old!.State);
        Assert.Equal("api-cert", _prefs.Connections[0].AzureCredentials!.CertificateRef!.CertificateName);
        _uploadMock.Verify(u => u.RemoveKeyCredentialAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Renew_AlreadySuperseded_Fails()
    {
        await CreateCertAsync();
        await _certService.MarkSupersededAsync("api-cert", "api-cert-r2");

        var result = await CreateService().RenewAsync("api-cert", removeOldKeyCredential: false);

        Assert.False(result.Success);
        Assert.Contains("already been renewed", result.Steps.First(s => s.Id == "load").Message);
    }

    [Fact]
    public async Task Renew_SuccessorNameIncrements()
    {
        await CreateCertAsync();
        var first = await CreateService().RenewAsync("api-cert", removeOldKeyCredential: false);
        Assert.Equal("api-cert-r2", first.Certificate!.Name);

        var second = await CreateService().RenewAsync("api-cert-r2", removeOldKeyCredential: false);
        Assert.Equal("api-cert-r3", second.Certificate!.Name);
    }
}
