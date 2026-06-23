using Garrard.Mcp.Explorer.Cli.Runbooks;
using Garrard.Mcp.Explorer.Core.Domain.HttpApi;
using Garrard.Mcp.Explorer.Core.Interfaces;
using Moq;

namespace Garrard.Tests.Mcp.Explorer.Cli.Runbooks;

public class HttpRunbookExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_ChainsStepBodyToNextStepInput()
    {
        var endpoint = new HttpApiDefinition { Id = "e1", Name = "Weather API", BaseUrl = "https://example", Path = "/weather" };
        var store = new Mock<IHttpApiStore>();
        store.Setup(s => s.GetAllDefinitionsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([endpoint]);

        IReadOnlyDictionary<string, string?>? secondInputs = null;
        var call = 0;
        var invoker = new Mock<IHttpApiInvoker>();
        invoker
            .Setup(i => i.InvokeAsync(It.IsAny<HttpApiDefinition>(), It.IsAny<IReadOnlyDictionary<string, string?>>(), It.IsAny<CancellationToken>()))
            .Returns((HttpApiDefinition _, IReadOnlyDictionary<string, string?> inputs, CancellationToken _) =>
            {
                call++;
                if (call == 1)
                {
                    return Task.FromResult(new HttpApiInvokeResult
                    {
                        StatusCode = 200,
                        Body = "{\"message\":\"hello\"}",
                        TruncatedBody = "{\"message\":\"hello\"}"
                    });
                }

                secondInputs = inputs;
                return Task.FromResult(new HttpApiInvokeResult { StatusCode = 200, Body = "ok", TruncatedBody = "ok" });
            });

        var executor = new HttpRunbookExecutor(store.Object, invoker.Object, new McpRunbookTemplateResolver());

        var runbook = new HttpRunbook
        {
            Steps =
            [
                new HttpRunbookStep { Id = "first", Endpoint = "Weather API" },
                new HttpRunbookStep
                {
                    Id = "second",
                    Endpoint = "Weather API",
                    Inputs = new Dictionary<string, object?> { ["value"] = "{{ steps.first.body.message }}" }
                }
            ]
        };

        var results = await executor.ExecuteAsync(runbook, _ => { }, CancellationToken.None);

        Assert.Equal("hello", secondInputs!["value"]);
        Assert.True(results.ContainsKey("second"));
    }

    [Fact]
    public async Task ExecuteAsync_UsesSchedulingRepeat()
    {
        var endpoint = new HttpApiDefinition { Id = "e1", Name = "Weather API", BaseUrl = "https://example", Path = "/weather" };
        var store = new Mock<IHttpApiStore>();
        store.Setup(s => s.GetAllDefinitionsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([endpoint]);

        var invoker = new Mock<IHttpApiInvoker>();
        invoker
            .Setup(i => i.InvokeAsync(It.IsAny<HttpApiDefinition>(), It.IsAny<IReadOnlyDictionary<string, string?>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HttpApiInvokeResult { StatusCode = 200, Body = "{}", TruncatedBody = "{}" });

        var executor = new HttpRunbookExecutor(store.Object, invoker.Object, new McpRunbookTemplateResolver());

        var runbook = new HttpRunbook
        {
            Schedule = new HttpRunbookSchedule { Repeat = 3 },
            Steps = [new HttpRunbookStep { Id = "step1", Endpoint = "Weather API" }]
        };

        await executor.ExecuteAsync(runbook, _ => { }, CancellationToken.None);

        invoker.Verify(i => i.InvokeAsync(It.IsAny<HttpApiDefinition>(), It.IsAny<IReadOnlyDictionary<string, string?>>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }
}
