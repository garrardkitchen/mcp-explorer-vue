using System.Text;
using Garrard.Mcp.Explorer.Api.Controllers.v1;
using Garrard.Mcp.Explorer.Core.Domain.DevTunnels;
using Garrard.Mcp.Explorer.Core.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;

namespace Garrard.Tests.Mcp.Explorer.Api.Controllers;

public sealed class WebhookCaptureControllerTests
{
    [Fact]
    public async Task Capture_MapsRequestIntoWebhookCaptureRequest()
    {
        var serviceMock = new Mock<IDevTunnelService>();
        var controller = new WebhookCaptureController(serviceMock.Object, CreateConfiguration());

        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.ContentType = "application/json";
        context.Request.QueryString = new QueryString("?run=1");
        context.Request.Headers["Authorization"] = "Bearer secret";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("""{"hello":"world"}"""));
        controller.ControllerContext = new ControllerContext { HttpContext = context };

        var result = await controller.Capture("tunnel-1", "callbacks/run", CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        serviceMock.Verify(s => s.CaptureAsync(
            It.Is<WebhookCaptureRequest>(request =>
                request.TunnelId == "tunnel-1"
                && request.Method == HttpMethods.Post
                && request.Path == "/callbacks/run"
                && request.QueryString == "?run=1"
                && request.ContentType == "application/json"
                && request.Headers["Authorization"] == "Bearer secret"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Capture_WhenTunnelMissing_Returns404()
    {
        var serviceMock = new Mock<IDevTunnelService>();
        serviceMock.Setup(s => s.CaptureAsync(It.IsAny<WebhookCaptureRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException());

        var controller = new WebhookCaptureController(serviceMock.Object, CreateConfiguration())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    Request =
                    {
                        Method = HttpMethods.Get,
                        Body = new MemoryStream()
                    }
                }
            }
        };

        var result = await controller.Capture("missing", null, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Capture_WhenPayloadExceedsConfiguredLimit_Returns413()
    {
        var serviceMock = new Mock<IDevTunnelService>();
        var controller = new WebhookCaptureController(serviceMock.Object, CreateConfiguration(maxCaptureBytes: 4))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    Request =
                    {
                        Method = HttpMethods.Post,
                        Body = new MemoryStream(Encoding.UTF8.GetBytes("abcdef")),
                        ContentLength = 6
                    }
                }
            }
        };

        var result = await controller.Capture("tunnel-1", null, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status413PayloadTooLarge, objectResult.StatusCode);
        serviceMock.Verify(s => s.CaptureAsync(It.IsAny<WebhookCaptureRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static IConfiguration CreateConfiguration(int maxCaptureBytes = 1024 * 1024)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DevTunnels:MaxCaptureBytes"] = maxCaptureBytes.ToString()
            })
            .Build();
}
