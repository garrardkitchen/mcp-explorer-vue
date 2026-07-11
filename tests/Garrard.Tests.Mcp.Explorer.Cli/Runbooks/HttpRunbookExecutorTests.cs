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

        var secretResolver = new Mock<IKeyVaultSecretResolver>();
        var executor = new HttpRunbookExecutor(
            store.Object,
            invoker.Object,
            new HttpRunbookConnectionFactory(secretResolver.Object),
            new RunbookTemplateResolver());

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

        var execution = await executor.ExecuteAsync(runbook, _ => { }, CancellationToken.None);

        Assert.Equal("hello", secondInputs!["value"]);
        Assert.True(execution.Results.ContainsKey("second"));
        Assert.Equal(0, execution.FailureCount);
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

        var secretResolver = new Mock<IKeyVaultSecretResolver>();
        var executor = new HttpRunbookExecutor(
            store.Object,
            invoker.Object,
            new HttpRunbookConnectionFactory(secretResolver.Object),
            new RunbookTemplateResolver());

        var runbook = new HttpRunbook
        {
            Schedule = new RunbookSchedule { Repeat = 3 },
            Steps = [new HttpRunbookStep { Id = "step1", Endpoint = "Weather API" }]
        };

        await executor.ExecuteAsync(runbook, _ => { }, CancellationToken.None);

        invoker.Verify(i => i.InvokeAsync(It.IsAny<HttpApiDefinition>(), It.IsAny<IReadOnlyDictionary<string, string?>>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task ExecuteAsync_AssertionFailureCanContinue_WhenConfigured()
    {
        var endpoint = new HttpApiDefinition { Id = "e1", Name = "Weather API", BaseUrl = "https://example", Path = "/weather" };
        var store = new Mock<IHttpApiStore>();
        store.Setup(s => s.GetAllDefinitionsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([endpoint]);

        var invoker = new Mock<IHttpApiInvoker>();
        invoker
            .Setup(i => i.InvokeAsync(It.IsAny<HttpApiDefinition>(), It.IsAny<IReadOnlyDictionary<string, string?>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HttpApiInvokeResult { StatusCode = 500, Body = "{\"error\":\"boom\"}", TruncatedBody = "{\"error\":\"boom\"}" });

        var secretResolver = new Mock<IKeyVaultSecretResolver>();
        var executor = new HttpRunbookExecutor(
            store.Object,
            invoker.Object,
            new HttpRunbookConnectionFactory(secretResolver.Object),
            new RunbookTemplateResolver());

        var runbook = new HttpRunbook
        {
            ContinueOnAssertFailure = true,
            Steps =
            [
                new HttpRunbookStep
                {
                    Id = "step1",
                    Endpoint = "Weather API",
                    Assert = new RunbookAssertion { Path = "isSuccess", Operator = "equals", Value = true }
                }
            ]
        };

        var execution = await executor.ExecuteAsync(runbook, _ => { }, CancellationToken.None);

        Assert.Equal(1, execution.FailureCount);
        Assert.True(execution.Results.ContainsKey("step1"));
    }

    [Fact]
    public async Task ExecuteAsync_UsesInlineConnectionFromRunbook_WhenDefaultConnectionIsSet()
    {
        var store = new Mock<IHttpApiStore>();
        store.Setup(s => s.GetAllDefinitionsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        HttpApiDefinition? resolvedDefinition = null;
        var invoker = new Mock<IHttpApiInvoker>();
        invoker
            .Setup(i => i.InvokeAsync(It.IsAny<HttpApiDefinition>(), It.IsAny<IReadOnlyDictionary<string, string?>>(), It.IsAny<CancellationToken>()))
            .Returns((HttpApiDefinition definition, IReadOnlyDictionary<string, string?> _, CancellationToken _) =>
            {
                resolvedDefinition = definition;
                return Task.FromResult(new HttpApiInvokeResult { StatusCode = 200, Body = "{}", TruncatedBody = "{}" });
            });

        var secretResolver = new Mock<IKeyVaultSecretResolver>();
        var executor = new HttpRunbookExecutor(
            store.Object,
            invoker.Object,
            new HttpRunbookConnectionFactory(secretResolver.Object),
            new RunbookTemplateResolver());

        var runbook = new HttpRunbook
        {
            DefaultConnection = "httpbin",
            Connections =
            [
                new HttpRunbookConnection
                {
                    Name = "httpbin",
                    Endpoint = "https://httpbin.org/get",
                    Auth = new HttpRunbookAuth
                    {
                        Type = "custom",
                        Headers =
                        [
                            new HttpRunbookHeader
                            {
                                Name = "X-Test-Header",
                                Value = "inline"
                            }
                        ]
                    }
                }
            ],
            Steps = [new HttpRunbookStep { Id = "step1" }]
        };

        await executor.ExecuteAsync(runbook, _ => { }, CancellationToken.None);

        Assert.NotNull(resolvedDefinition);
        Assert.Equal("httpbin", resolvedDefinition!.Name);
        Assert.Equal("https://httpbin.org/get", resolvedDefinition.BaseUrl);
        Assert.Equal("GET", resolvedDefinition.Method);
        Assert.Equal("inline", resolvedDefinition.Headers.Single(h => h.Name == "X-Test-Header").Value);
        Assert.Equal(HttpApiAuthenticationMode.CustomHeaders, resolvedDefinition.AuthenticationMode);
    }

    [Fact]
    public async Task ExecuteAsync_UseLocalhost_DoesNotMutateSharedInlineConnection()
    {
        var store = new Mock<IHttpApiStore>();
        store.Setup(s => s.GetAllDefinitionsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var baseUrls = new List<string>();
        var invoker = new Mock<IHttpApiInvoker>();
        invoker
            .Setup(i => i.InvokeAsync(It.IsAny<HttpApiDefinition>(), It.IsAny<IReadOnlyDictionary<string, string?>>(), It.IsAny<CancellationToken>()))
            .Returns((HttpApiDefinition definition, IReadOnlyDictionary<string, string?> _, CancellationToken _) =>
            {
                baseUrls.Add(definition.BaseUrl);
                return Task.FromResult(new HttpApiInvokeResult { StatusCode = 200, Body = "{}", TruncatedBody = "{}" });
            });

        var secretResolver = new Mock<IKeyVaultSecretResolver>();
        var executor = new HttpRunbookExecutor(
            store.Object,
            invoker.Object,
            new HttpRunbookConnectionFactory(secretResolver.Object),
            new RunbookTemplateResolver());

        var runbook = new HttpRunbook
        {
            DefaultConnection = "local-service",
            Connections =
            [
                new HttpRunbookConnection
                {
                    Name = "local-service",
                    Endpoint = "http://host.docker.internal:8080/health",
                    Auth = new HttpRunbookAuth { Type = "none" }
                }
            ],
            Steps =
            [
                new HttpRunbookStep { Id = "step1", UseLocalhost = true },
                new HttpRunbookStep { Id = "step2", UseLocalhost = false }
            ]
        };

        await executor.ExecuteAsync(runbook, _ => { }, CancellationToken.None);

        Assert.Equal("http://localhost:8080/health", baseUrls[0]);
        Assert.Equal("http://host.docker.internal:8080/health", baseUrls[1]);
    }

    [Fact]
    public async Task ExecuteAsync_UsesInlineConnectionPartialNameMatch()
    {
        var store = new Mock<IHttpApiStore>();
        store.Setup(s => s.GetAllDefinitionsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        HttpApiDefinition? resolvedDefinition = null;
        var invoker = new Mock<IHttpApiInvoker>();
        invoker
            .Setup(i => i.InvokeAsync(It.IsAny<HttpApiDefinition>(), It.IsAny<IReadOnlyDictionary<string, string?>>(), It.IsAny<CancellationToken>()))
            .Returns((HttpApiDefinition definition, IReadOnlyDictionary<string, string?> _, CancellationToken _) =>
            {
                resolvedDefinition = definition;
                return Task.FromResult(new HttpApiInvokeResult { StatusCode = 200, Body = "{}", TruncatedBody = "{}" });
            });

        var secretResolver = new Mock<IKeyVaultSecretResolver>();
        var executor = new HttpRunbookExecutor(
            store.Object,
            invoker.Object,
            new HttpRunbookConnectionFactory(secretResolver.Object),
            new RunbookTemplateResolver());

        var runbook = new HttpRunbook
        {
            Connections =
            [
                new HttpRunbookConnection
                {
                    Name = "HttpBin Status",
                    Endpoint = "https://httpbin.org/status/{code}",
                    Auth = new HttpRunbookAuth { Type = "custom", Headers = [] }
                }
            ],
            Steps =
            [
                new HttpRunbookStep
                {
                    Id = "step1",
                    Endpoint = "Status",
                    Inputs = new Dictionary<string, object?> { ["code"] = "200" }
                }
            ]
        };

        await executor.ExecuteAsync(runbook, _ => { }, CancellationToken.None);

        Assert.NotNull(resolvedDefinition);
        Assert.Equal("HttpBin Status", resolvedDefinition!.Name);
    }
}
