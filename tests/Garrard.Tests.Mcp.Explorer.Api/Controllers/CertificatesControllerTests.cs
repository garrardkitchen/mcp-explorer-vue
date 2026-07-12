using Garrard.Mcp.Explorer.Api.Controllers.v1;
using Garrard.Mcp.Explorer.Api.Dtos.Certificates;
using Garrard.Mcp.Explorer.Core.Domain.Certificates;
using Garrard.Mcp.Explorer.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Garrard.Tests.Mcp.Explorer.Api.Controllers;

public sealed class CertificatesControllerTests
{
    private readonly Mock<ICertificateService> _certServiceMock = new();
    private readonly Mock<ICertificateUploadService> _uploadServiceMock = new();
    private readonly Mock<ICertificateRenewalService> _renewalServiceMock = new();
    private readonly Mock<IKeyVaultCertificateService> _keyVaultServiceMock = new();
    private readonly Garrard.Mcp.Explorer.Infrastructure.Certificates.CertificateNotificationState _notificationState = new();

    private CertificatesController CreateController()
        => new(_certServiceMock.Object, _uploadServiceMock.Object, _renewalServiceMock.Object, _keyVaultServiceMock.Object, _notificationState);

    private static CertificateInfo SampleCert(string name = "test-cert") => new()
    {
        Name = name,
        Subject = $"CN=mcp-explorer-{name}",
        KeySize = 2048,
        Source = CertificateSource.SelfSigned,
        State = CertificateState.Active,
        CreatedAt = DateTimeOffset.UtcNow,
        NotAfter = DateTimeOffset.UtcNow.AddMonths(12),
        ThumbprintSha1 = "AABBCC",
    };

    [Fact]
    public async Task GetAll_ReturnsCertificatesWithUsage()
    {
        _certServiceMock.Setup(s => s.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([SampleCert()]);
        _certServiceMock.Setup(s => s.GetUsageAsync("test-cert", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CertificateUsage { CertificateName = "test-cert", ConnectionNames = ["conn-1"] });

        var result = await CreateController().GetAll(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
    }

    [Fact]
    public async Task Get_UnknownName_ReturnsNotFound()
    {
        _certServiceMock.Setup(s => s.GetAsync("missing", It.IsAny<CancellationToken>())).ReturnsAsync((CertificateInfo?)null);

        var result = await CreateController().Get("missing", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Get_InvalidName_ReturnsBadRequest()
    {
        _certServiceMock.Setup(s => s.GetAsync("../evil", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Certificate name must be..."));

        var result = await CreateController().Get("../evil", CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Delete_InUse_Returns409WithUsage()
    {
        var usage = new CertificateUsage
        {
            CertificateName = "test-cert",
            ConnectionNames = ["finance-mcp"],
            HttpApiNames = ["finance-http"],
        };
        _certServiceMock.Setup(s => s.DeleteAsync("test-cert", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CertificateInUseException(usage));

        var result = await CreateController().Delete("test-cert", CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Contains("finance-mcp", System.Text.Json.JsonSerializer.Serialize(conflict.Value));
    }

    [Fact]
    public async Task Delete_Unreferenced_ReturnsNoContent()
    {
        var result = await CreateController().Delete("test-cert", CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        _certServiceMock.Verify(s => s.DeleteAsync("test-cert", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Generate_ReturnsOperationResult()
    {
        var opResult = OperationResult.Succeeded([new StepResult("validate", "Validate request", StepStatus.Succeeded)], SampleCert());
        _certServiceMock.Setup(s => s.GenerateSelfSignedAsync(It.IsAny<GenerateCertificateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(opResult);

        var result = await CreateController().Generate(new GenerateCertificateApiRequest("test-cert", null), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var returned = Assert.IsType<OperationResult>(ok.Value);
        Assert.True(returned.Success);
    }

    [Fact]
    public async Task Upload_MissingAppId_ReturnsBadRequest()
    {
        var result = await CreateController().Upload("test-cert", new UploadCertificateRequest(""), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        _uploadServiceMock.Verify(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Upload_MapsStepFailures()
    {
        var failed = OperationResult.Failed(
        [
            new StepResult("load-cert", "Load certificate from the local store", StepStatus.Succeeded),
            new StepResult("fetch-app", "Fetch the app registration via Microsoft Graph", StepStatus.Failed, "Insufficient Microsoft Graph permissions."),
            new StepResult("add-key", "Add the certificate to keyCredentials", StepStatus.Skipped),
        ]);
        _uploadServiceMock.Setup(s => s.UploadAsync("test-cert", "11111111-1111-1111-1111-111111111111", It.IsAny<CancellationToken>()))
            .ReturnsAsync(failed);

        var result = await CreateController().Upload("test-cert",
            new UploadCertificateRequest("11111111-1111-1111-1111-111111111111"), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var returned = Assert.IsType<OperationResult>(ok.Value);
        Assert.False(returned.Success);
        Assert.Equal(StepStatus.Failed, returned.Steps.First(s => s.Id == "fetch-app").Status);
    }

    [Fact]
    public async Task ExportPfx_MissingPassword_ReturnsBadRequest()
    {
        var result = await CreateController().ExportPfx("test-cert", new ExportPfxRequest(""), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        _certServiceMock.Verify(s => s.ExportPfxAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExportPfx_ReturnsFile()
    {
        _certServiceMock.Setup(s => s.ExportPfxAsync("test-cert", "pw", It.IsAny<CancellationToken>()))
            .ReturnsAsync([1, 2, 3]);

        var result = await CreateController().ExportPfx("test-cert", new ExportPfxRequest("pw"), CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/x-pkcs12", file.ContentType);
        Assert.Equal("test-cert.pfx", file.FileDownloadName);
    }

    [Fact]
    public async Task DownloadPem_ReturnsPublicPemFile()
    {
        _certServiceMock.Setup(s => s.GetPublicCertPemAsync("test-cert", It.IsAny<CancellationToken>()))
            .ReturnsAsync("-----BEGIN CERTIFICATE-----\nabc\n-----END CERTIFICATE-----");

        var result = await CreateController().DownloadPem("test-cert", CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("test-cert.pem", file.FileDownloadName);
        Assert.DoesNotContain("PRIVATE KEY", System.Text.Encoding.UTF8.GetString(file.FileContents));
    }

    [Fact]
    public async Task TestToken_MissingFields_ReturnsBadRequest()
    {
        var result = await CreateController().TestToken("test-cert", new TestTokenRequest("", "client", "scope"), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetExpiring_InvalidDays_ReturnsBadRequest()
    {
        var result = await CreateController().GetExpiring(0, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task UploadStatus_ReturnsStatusString()
    {
        _uploadServiceMock.Setup(s => s.VerifyUploadAsync("test-cert", "11111111-1111-1111-1111-111111111111", It.IsAny<CancellationToken>()))
            .ReturnsAsync(UploadStatus.Stale);

        var result = await CreateController().UploadStatus("test-cert", "11111111-1111-1111-1111-111111111111", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Contains("Stale", System.Text.Json.JsonSerializer.Serialize(ok.Value));
    }
}
