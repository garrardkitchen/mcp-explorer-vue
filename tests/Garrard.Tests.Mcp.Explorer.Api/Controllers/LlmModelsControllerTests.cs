using Garrard.Mcp.Explorer.Api.Controllers.v1;
using Garrard.Mcp.Explorer.Core.Domain.LlmModels;
using Garrard.Mcp.Explorer.Core.Domain.Preferences;
using Garrard.Mcp.Explorer.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Garrard.Tests.Mcp.Explorer.Api.Controllers;

public sealed class LlmModelsControllerTests
{
    private readonly Mock<IUserPreferencesStore> _store = new();
    private readonly Mock<IAiChatService> _chatService = new();

    [Fact]
    public async Task Test_ConfiguredModel_ReturnsSuccessfulProbeResult()
    {
        var model = new LlmModelDefinition { Name = "foundry-agent" };
        _store.Setup(x => x.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserPreferences { LlmModels = [model] });
        _chatService.Setup(x => x.TestAsync(model, It.IsAny<CancellationToken>()))
            .ReturnsAsync("OK");
        var controller = new LlmModelsController(_store.Object, _chatService.Object);

        var result = await controller.Test("FOUNDRY-AGENT", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(true, ok.Value!.GetType().GetProperty("success")!.GetValue(ok.Value));
        Assert.Equal("OK", ok.Value.GetType().GetProperty("message")!.GetValue(ok.Value));
    }

    [Fact]
    public async Task Test_UnknownModel_ReturnsNotFoundWithoutProbe()
    {
        _store.Setup(x => x.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserPreferences());
        var controller = new LlmModelsController(_store.Object, _chatService.Object);

        var result = await controller.Test("missing", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        _chatService.Verify(
            x => x.TestAsync(It.IsAny<LlmModelDefinition>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Test_ProbeFailure_ReturnsBadRequestWithoutExposingInnerObject()
    {
        var model = new LlmModelDefinition { Name = "foundry-agent" };
        _store.Setup(x => x.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserPreferences { LlmModels = [model] });
        _chatService.Setup(x => x.TestAsync(model, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Agent version was not found."));
        var controller = new LlmModelsController(_store.Object, _chatService.Object);

        var result = await controller.Test(model.Name, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(
            "Agent version was not found.",
            badRequest.Value!.GetType().GetProperty("error")!.GetValue(badRequest.Value));
    }

    [Fact]
    public async Task Test_ProbeFailure_PreservesTopLevelDiagnosticContext()
    {
        var model = new LlmModelDefinition { Name = "foundry-agent" };
        _store.Setup(x => x.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserPreferences { LlmModels = [model] });
        _chatService.Setup(x => x.TestAsync(model, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(
                "DefaultAzureCredential failed to retrieve a token.",
                new HttpRequestException("Connection refused (169.254.169.254:80).")));
        var controller = new LlmModelsController(_store.Object, _chatService.Object);

        var result = await controller.Test(model.Name, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(
            "DefaultAzureCredential failed to retrieve a token.",
            badRequest.Value!.GetType().GetProperty("error")!.GetValue(badRequest.Value));
    }
}
