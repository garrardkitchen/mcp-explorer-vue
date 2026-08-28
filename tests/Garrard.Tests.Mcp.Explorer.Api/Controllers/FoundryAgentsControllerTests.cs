using Garrard.Mcp.Explorer.Api.Controllers.v1;
using Garrard.Mcp.Explorer.Core.Domain.LlmModels;
using Garrard.Mcp.Explorer.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Garrard.Tests.Mcp.Explorer.Api.Controllers;

public sealed class FoundryAgentsControllerTests
{
    [Fact]
    public async Task Discover_ValidConfiguration_ReturnsAgentCatalog()
    {
        var model = new LlmModelDefinition { Endpoint = "https://example.test/api/projects/demo" };
        FoundryAgentCatalogItem[] catalog =
        [
            new("codie",
            [
                new("5", "Be helpful.", "Current", DateTimeOffset.UnixEpoch)
            ])
        ];
        var service = new Mock<IFoundryProjectAgentService>();
        service.Setup(item => item.DiscoverAgentsAsync(model, It.IsAny<CancellationToken>()))
            .ReturnsAsync(catalog);
        var controller = new FoundryAgentsController(service.Object);

        var result = await controller.Discover(model, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(catalog, ok.Value);
    }

    [Fact]
    public async Task Discover_Failure_ReturnsTopLevelDiagnostic()
    {
        var model = new LlmModelDefinition();
        var service = new Mock<IFoundryProjectAgentService>();
        service.Setup(item => item.DiscoverAgentsAsync(model, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Project access was denied."));
        var controller = new FoundryAgentsController(service.Object);

        var result = await controller.Discover(model, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(
            "Project access was denied.",
            badRequest.Value!.GetType().GetProperty("error")!.GetValue(badRequest.Value));
    }

    [Fact]
    public async Task Discover_RequestCancellation_Propagates()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var model = new LlmModelDefinition();
        var service = new Mock<IFoundryProjectAgentService>();
        service.Setup(item => item.DiscoverAgentsAsync(model, cancellation.Token))
            .Returns(Task.FromCanceled<IReadOnlyList<FoundryAgentCatalogItem>>(cancellation.Token));
        var controller = new FoundryAgentsController(service.Object);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => controller.Discover(model, cancellation.Token));
    }
}
