using Garrard.Mcp.Explorer.Api.Controllers.v1;
using Garrard.Mcp.Explorer.Api.Dtos.DevTunnels;
using Garrard.Mcp.Explorer.Core.Domain.DevTunnels;
using Garrard.Mcp.Explorer.Core.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Net;

namespace Garrard.Tests.Mcp.Explorer.Api.Controllers;

public sealed class DevTunnelsControllerTests
{
    private readonly Mock<IDevTunnelService> _serviceMock = new();
    private readonly DevTunnelsController _sut;

    public DevTunnelsControllerTests()
    {
        _sut = new DevTunnelsController(_serviceMock.Object);
    }

    [Fact]
    public async Task GetAll_ReturnsTunnelList()
    {
        _serviceMock.Setup(s => s.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new DevTunnel("t1", "Payments", TunnelAccess.Anonymous, TunnelStatus.Running, null, null, DateTime.UtcNow, null, null, null, false, 0)
            ]);

        var result = await _sut.GetAll(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var tunnels = Assert.IsAssignableFrom<IReadOnlyList<DevTunnel>>(ok.Value);
        Assert.Single(tunnels);
    }

    [Fact]
    public async Task Create_WhenServiceThrowsConflict_Returns409()
    {
        _serviceMock.Setup(s => s.CreateAsync(It.IsAny<CreateDevTunnelRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("exists"));

        var result = await _sut.Create(new CreateDevTunnelDto("Payments"), CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task Create_WhenCliUnavailable_Returns503()
    {
        _serviceMock.Setup(s => s.CreateAsync(It.IsAny<CreateDevTunnelRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DevTunnelCliUnavailableException("missing"));

        var result = await _sut.Create(new CreateDevTunnelDto("Payments"), CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, objectResult.StatusCode);
    }

    [Fact]
    public async Task Replay_WithInvalidTargetUrl_Returns400()
    {
        var result = await _sut.Replay("t1", "e1", new ReplayWebhookDto("not-a-url"), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Replay_WithLoopbackTargetUrl_Returns400()
    {
        var result = await _sut.Replay("t1", "e1", new ReplayWebhookDto("http://127.0.0.1/hooks"), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Replay_WithPublicTargetUrl_DelegatesToService()
    {
        _serviceMock.Setup(s => s.ReplayAsync(It.IsAny<ReplayWebhookRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReplayWebhookResult(202, "Accepted", new Dictionary<string, string>(), "ok", 2, TimeSpan.FromMilliseconds(10)));

        var result = await _sut.Replay("t1", "e1", new ReplayWebhookDto("https://1.1.1.1/hooks"), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        _serviceMock.Verify(s => s.ReplayAsync(
            It.Is<ReplayWebhookRequest>(request =>
                request.TargetUri == new Uri("https://1.1.1.1/hooks")
                && request.AllowedAddresses.Count == 1
                && Equals(request.AllowedAddresses[0], IPAddress.Parse("1.1.1.1"))),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Delete_WhenTunnelMissing_Returns404()
    {
        _serviceMock.Setup(s => s.DeleteAsync("missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _sut.Delete("missing", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task LoginStream_WhenCliUnavailable_Returns503()
    {
        _serviceMock.Setup(s => s.LoginWithDeviceCodeAsync(It.IsAny<CancellationToken>()))
            .Returns(ThrowUnavailable());

        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        var result = await _sut.LoginStream(CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, objectResult.StatusCode);
    }

    private static async IAsyncEnumerable<string> ThrowUnavailable()
    {
        await Task.Yield();
        throw new DevTunnelCliUnavailableException("missing");
#pragma warning disable CS0162
        yield break;
#pragma warning restore CS0162
    }
}
