using Garrard.Mcp.Explorer.Core.Domain.Chat;
using Garrard.Mcp.Explorer.Core.Domain.LlmModels;
using Garrard.Mcp.Explorer.Core.Interfaces;
using Garrard.Mcp.Explorer.Infrastructure.LlmProviders;
using Moq;
using OpenAI;
using OpenAI.Responses;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net;
using System.Text;
using System.Text.Json;

#pragma warning disable OPENAI001

namespace Garrard.Tests.Mcp.Explorer.Infrastructure.LlmProviders;

public sealed class FoundryProjectAgentServiceTests
{
    private readonly FoundryProjectAgentService _service = new();

    [Theory]
    [InlineData("")]
    [InlineData("not-a-uri")]
    [InlineData("http://resource.services.ai.azure.com/api/projects/project")]
    [InlineData("https://resource.services.ai.azure.com/")]
    public async Task TestAsync_InvalidEndpoint_Throws(string endpoint)
    {
        var model = ValidModel();
        model.Endpoint = endpoint;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.TestAsync(model));

        Assert.Contains("HTTPS Foundry project endpoint", exception.Message);
    }

    [Fact]
    public async Task TestAsync_MissingAgentName_Throws()
    {
        var model = ValidModel();
        model.AgentName = string.Empty;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.TestAsync(model));

        Assert.Contains("agent name", exception.Message);
    }

    [Fact]
    public async Task TestAsync_MissingAgentVersion_Throws()
    {
        var model = ValidModel();
        model.AgentVersion = string.Empty;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.TestAsync(model));

        Assert.Contains("agent version", exception.Message);
    }

    [Fact]
    public async Task TestAsync_ApiKeyModeWithoutKey_ThrowsBeforeNetworkCall()
    {
        var model = ValidModel();
        model.AuthenticationMode = LlmAuthenticationMode.ApiKey;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.TestAsync(model));

        Assert.Contains("API key is required", exception.Message);
    }

    [Fact]
    public void Validate_HostedAgentEndpoint_DoesNotRequireAgentVersion()
    {
        var model = ValidModel();
        model.AgentInvocationMode = FoundryAgentInvocationMode.HostedAgentEndpoint;
        model.AgentVersion = string.Empty;

        FoundryProjectAgentService.Validate(model);
    }

    [Fact]
    public async Task CreateResponsesClient_HostedAgentEndpoint_UsesDedicatedAgentRoute()
    {
        Uri? requestedUri = null;
        string? apiKeyHeader = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            requestedUri = request.RequestUri;
            apiKeyHeader = request.Headers.TryGetValues("api-key", out var values)
                ? values.Single()
                : null;
            return JsonResponse("""{"error":{"message":"stop after capturing URI"}}""", HttpStatusCode.BadRequest);
        });
        using var httpClient = new HttpClient(handler);
        var options = new OpenAIClientOptions
        {
            Transport = new HttpClientPipelineTransport(httpClient)
        };
        var model = ValidModel();
        model.AuthenticationMode = LlmAuthenticationMode.ApiKey;
        model.ApiKey = "test-key";
        model.AgentInvocationMode = FoundryAgentInvocationMode.HostedAgentEndpoint;
        model.AgentVersion = string.Empty;

        var responsesClient = FoundryProjectAgentService.CreateHostedApiKeyResponsesClient(model, options);
        var requestOptions = new CreateResponseOptions();
        requestOptions.InputItems.Add(ResponseItem.CreateUserMessageItem("Hello hosted agent."));
        await Assert.ThrowsAsync<ClientResultException>(
            () => responsesClient.CreateResponseAsync(requestOptions));

        Assert.NotNull(requestedUri);
        Assert.Equal(
            "/api/projects/project/agents/codie/endpoint/protocols/openai/responses",
            requestedUri.AbsolutePath);
        Assert.Equal("test-key", apiKeyHeader);
    }

    [Fact]
    public async Task DiscoverAgentsAsync_ApiKey_MapsVersionsAndInstructions()
    {
        var requestedUris = new List<Uri>();
        var apiKeyHeaders = new List<string?>();
        var handler = new StubHttpMessageHandler(request =>
        {
            requestedUris.Add(request.RequestUri!);
            apiKeyHeaders.Add(request.Headers.TryGetValues("api-key", out var values)
                ? values.Single()
                : null);

            var json = request.RequestUri!.AbsolutePath.EndsWith("/versions", StringComparison.Ordinal)
                ? """
                  {"data":[
                    {"version":"1","description":"Initial","created_at":100,"definition":{"kind":"prompt","instructions":"Prompt one"}},
                    {"version":"2","description":"Current","created_at":200,"definition":{"kind":"prompt","instructions":"Prompt two"}}
                  ],"has_more":false}
                  """
                : """{"data":[{"name":"codie"}],"has_more":false}""";
            return JsonResponse(json);
        });
        var httpClientFactory = CreateHttpClientFactory(handler);
        var service = new FoundryProjectAgentService(httpClientFactory: httpClientFactory.Object);
        var model = ValidModel();
        model.AuthenticationMode = LlmAuthenticationMode.ApiKey;
        model.ApiKey = "test-key";

        var agents = await service.DiscoverAgentsAsync(model);

        var agent = Assert.Single(agents);
        Assert.Equal("codie", agent.Name);
        Assert.Collection(
            agent.Versions,
            version =>
            {
                Assert.Equal("2", version.Version);
                Assert.Equal("Prompt two", version.SystemPrompt);
                Assert.Equal("Current", version.Description);
            },
            version =>
            {
                Assert.Equal("1", version.Version);
                Assert.Equal("Prompt one", version.SystemPrompt);
            });
        Assert.Equal(2, requestedUris.Count);
        Assert.All(apiKeyHeaders, value => Assert.Equal("test-key", value));
        Assert.Contains(requestedUris, uri => uri.AbsolutePath.EndsWith("/agents", StringComparison.Ordinal));
        Assert.Contains(requestedUris, uri => uri.AbsolutePath.EndsWith("/agents/codie/versions", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DiscoverAgentsAsync_PaginatedAgentList_FollowsContinuationCursor()
    {
        var agentListCalls = 0;
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/versions", StringComparison.Ordinal))
                return JsonResponse("""{"data":[],"has_more":false}""");

            agentListCalls++;
            return request.RequestUri.Query.Contains("after=agent-1", StringComparison.Ordinal)
                ? JsonResponse("""{"data":[{"name":"beta"}],"has_more":false}""")
                : JsonResponse("""{"data":[{"name":"alpha"}],"has_more":true,"last_id":"agent-1"}""");
        });
        var httpClientFactory = CreateHttpClientFactory(handler);
        var service = new FoundryProjectAgentService(httpClientFactory: httpClientFactory.Object);
        var model = ValidModel();
        model.AuthenticationMode = LlmAuthenticationMode.ApiKey;
        model.ApiKey = "test-key";

        var agents = await service.DiscoverAgentsAsync(model);

        Assert.Equal(["alpha", "beta"], agents.Select(agent => agent.Name));
        Assert.Equal(2, agentListCalls);
    }

    [Fact]
    public async Task DiscoverAgentsAsync_Cancellation_Propagates()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var handler = new StubHttpMessageHandler(_ => throw new InvalidOperationException("Handler should not complete."));
        var httpClientFactory = CreateHttpClientFactory(handler);
        var service = new FoundryProjectAgentService(httpClientFactory: httpClientFactory.Object);
        var model = ValidModel();
        model.AuthenticationMode = LlmAuthenticationMode.ApiKey;
        model.ApiKey = "test-key";

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.DiscoverAgentsAsync(model, cancellation.Token));
    }

    [Fact]
    public void FindPreviousResponseId_LatestSameModelAssistant_ReturnsProviderId()
    {
        var model = ValidModel();
        model.Name = "search";
        ChatMessage[] history =
        [
            new() { Role = "user", Content = "search for human" },
            new()
            {
                Role = "assistant",
                Content = "Choose a document.",
                ModelName = "search",
                ProviderResponseId = "resp_123"
            }
        ];

        var responseId = FoundryProjectAgentService.FindPreviousResponseId(history, model);

        Assert.Equal("resp_123", responseId);
    }

    [Fact]
    public void FindPreviousResponseId_DifferentModel_ReturnsNull()
    {
        var model = ValidModel();
        model.Name = "search";
        ChatMessage[] history =
        [
            new()
            {
                Role = "assistant",
                Content = "Choose a document.",
                ModelName = "another-model",
                ProviderResponseId = "resp_123"
            }
        ];

        Assert.Null(FoundryProjectAgentService.FindPreviousResponseId(history, model));
    }

    [Fact]
    public void CreateManagedMcpEvents_MapsCallAndResult()
    {
        var item = new McpToolCallItem(
            "documents",
            "search",
            BinaryData.FromString("{\"query\":\"human\"}"))
        {
            ToolOutput = "{\"matches\":3}"
        };

        var events = FoundryProjectAgentService.CreateManagedMcpEvents([item]);

        Assert.Collection(
            events,
            call =>
            {
                Assert.Equal(ChatStreamEventType.ToolCall, call.Type);
                Assert.Equal("search", call.ToolName);
                Assert.Equal("documents", call.ConnectionName);
                Assert.Contains("human", call.ToolParameters);
            },
            result =>
            {
                Assert.Equal(ChatStreamEventType.ToolResult, result.Type);
                Assert.Equal("search", result.ToolName);
                Assert.Equal("{\"matches\":3}", result.ToolResult);
            });
    }

    [Fact]
    public void ParseFunctionArguments_Object_PreservesStructuredValues()
    {
        var arguments = FoundryProjectAgentService.ParseFunctionArguments(
            """{"filter":"Ada","options":{"limit":2},"enabled":true}""");

        Assert.Equal("Ada", Assert.IsType<JsonElement>(arguments["filter"]).GetString());
        Assert.Equal(2, Assert.IsType<JsonElement>(arguments["options"])
            .GetProperty("limit").GetInt32());
        Assert.True(Assert.IsType<JsonElement>(arguments["enabled"]).GetBoolean());
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("\"text\"")]
    public void ParseFunctionArguments_NonObject_Throws(string json)
    {
        var exception = Assert.Throws<JsonException>(
            () => FoundryProjectAgentService.ParseFunctionArguments(json));

        Assert.Contains("JSON object", exception.Message);
    }

    [Fact]
    public async Task InvokeMcpToolAsync_UsesConnectionServiceResult()
    {
        var connections = new Mock<IConnectionService>();
        connections
            .Setup(c => c.InvokeToolAsync(
                "docs",
                "search",
                It.IsAny<Dictionary<string, object?>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("""{"matches":2}""");
        var service = new FoundryProjectAgentService(connections.Object);

        var result = await service.InvokeMcpToolAsync(
            "docs", "search", """{"query":"identity"}""", CancellationToken.None);

        Assert.Equal("""{"matches":2}""", result.Output);
        Assert.Null(result.Exception);
        connections.Verify(c => c.InvokeToolAsync(
            "docs",
            "search",
            It.Is<Dictionary<string, object?>>(a => a.ContainsKey("query")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InvokeMcpToolAsync_Failure_ReturnsErrorOutput()
    {
        var connections = new Mock<IConnectionService>();
        connections
            .Setup(c => c.InvokeToolAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object?>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Tool unavailable"));
        var service = new FoundryProjectAgentService(connections.Object);

        var result = await service.InvokeMcpToolAsync(
            "docs", "search", "{}", CancellationToken.None);

        Assert.IsType<InvalidOperationException>(result.Exception);
        using var output = JsonDocument.Parse(result.Output);
        Assert.True(output.RootElement.GetProperty("isError").GetBoolean());
        Assert.Equal("Tool unavailable", output.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task InvokeMcpToolAsync_Cancellation_Propagates()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var connections = new Mock<IConnectionService>();
        connections
            .Setup(c => c.InvokeToolAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object?>>(),
                cancellation.Token))
            .Returns(Task.FromCanceled<string>(cancellation.Token));
        var service = new FoundryProjectAgentService(connections.Object);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.InvokeMcpToolAsync(
            "docs", "search", "{}", cancellation.Token));
    }

    private static LlmModelDefinition ValidModel() => new()
    {
        ProviderType = LlmProviderTypes.AzureAiFoundryProject,
        Endpoint = "https://resource.services.ai.azure.com/api/projects/project",
        AgentName = "codie",
        AgentVersion = "5"
    };

    private static Mock<IHttpClientFactory> CreateHttpClientFactory(HttpMessageHandler handler)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(item => item.CreateClient("FoundryProjectAgentDiscovery"))
            .Returns(new HttpClient(handler));
        return factory;
    }

    private static HttpResponseMessage JsonResponse(
        string json,
        HttpStatusCode statusCode = HttpStatusCode.OK) => new(statusCode)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(handler(request));
        }
    }
}
