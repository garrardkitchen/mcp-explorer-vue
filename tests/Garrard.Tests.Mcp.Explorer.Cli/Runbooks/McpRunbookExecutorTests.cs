using Garrard.Mcp.Explorer.Cli.Runbooks;
using Garrard.Mcp.Explorer.Core.Domain.Preferences;
using Garrard.Mcp.Explorer.Core.Interfaces;
using Moq;

namespace Garrard.Tests.Mcp.Explorer.Cli.Runbooks;

public class McpRunbookExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_ChainsStepOutputToNextStep()
    {
        var connectionService = new Mock<IConnectionService>();
        connectionService.Setup(s => s.ConnectAsync(It.IsAny<Garrard.Mcp.Explorer.Core.Domain.Connections.ConnectionDefinition>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<IActiveConnection>());

        Dictionary<string, object?>? secondParams = null;
        var call = 0;
        connectionService
            .Setup(s => s.InvokeToolAsync("local", It.IsAny<string>(), It.IsAny<Dictionary<string, object?>>(), It.IsAny<CancellationToken>()))
            .Returns((string _, string __, Dictionary<string, object?> p, CancellationToken ___) =>
            {
                call++;
                if (call == 1) return Task.FromResult("{\"message\":\"hello\"}");
                secondParams = p;
                return Task.FromResult("ok");
            });

        var prefsStore = new Mock<IUserPreferencesStore>();
        prefsStore.Setup(p => p.LoadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new UserPreferences());
        var secretResolver = new Mock<IKeyVaultSecretResolver>(MockBehavior.Strict);

        var executor = new McpRunbookExecutor(
            connectionService.Object,
            prefsStore.Object,
            new McpRunbookConnectionFactory(secretResolver.Object),
            new McpRunbookTemplateResolver());

        var runbook = new McpRunbook
        {
            DefaultConnection = "local",
            Connections =
            [
                new McpRunbookConnection
                {
                    Name = "local",
                    Endpoint = "https://example",
                    Auth = new McpRunbookAuth { Type = "bearer", Token = "t" }
                }
            ],
            Steps =
            [
                new McpRunbookStep { Id = "first", Tool = "tool1" },
                new McpRunbookStep
                {
                    Id = "second",
                    Tool = "tool2",
                    Params = new Dictionary<string, object?> { ["value"] = "{{ steps.first.message }}" }
                }
            ]
        };

        var results = await executor.ExecuteAsync(runbook, _ => { }, CancellationToken.None);

        Assert.Equal("hello", secondParams!["value"]?.ToString());
        Assert.True(results.ContainsKey("second"));
    }

    [Fact]
    public async Task ExecuteAsync_UsesSchedulingRepeat()
    {
        var connectionService = new Mock<IConnectionService>();
        connectionService.Setup(s => s.ConnectAsync(It.IsAny<Garrard.Mcp.Explorer.Core.Domain.Connections.ConnectionDefinition>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<IActiveConnection>());
        connectionService.Setup(s => s.InvokeToolAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, object?>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("ok");

        var prefsStore = new Mock<IUserPreferencesStore>();
        prefsStore.Setup(p => p.LoadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new UserPreferences());
        var secretResolver = new Mock<IKeyVaultSecretResolver>(MockBehavior.Strict);

        var executor = new McpRunbookExecutor(
            connectionService.Object,
            prefsStore.Object,
            new McpRunbookConnectionFactory(secretResolver.Object),
            new McpRunbookTemplateResolver());

        var runbook = new McpRunbook
        {
            DefaultConnection = "local",
            Schedule = new McpRunbookSchedule { Repeat = 3 },
            Connections =
            [
                new McpRunbookConnection
                {
                    Name = "local",
                    Endpoint = "https://example",
                    Auth = new McpRunbookAuth { Type = "bearer", Token = "t" }
                }
            ],
            Steps = [new McpRunbookStep { Id = "step1", Tool = "tool1" }]
        };

        await executor.ExecuteAsync(runbook, _ => { }, CancellationToken.None);

        connectionService.Verify(s => s.InvokeToolAsync("local", "tool1", It.IsAny<Dictionary<string, object?>>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }
}
