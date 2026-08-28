using System.Runtime.CompilerServices;
using System.Net.Http.Headers;
using System.Text.Json;
using Azure.Core;
using Azure.AI.Extensions.OpenAI;
using Azure.AI.Projects;
using Azure.Identity;
using CoreChatMessage = Garrard.Mcp.Explorer.Core.Domain.Chat.ChatMessage;
using Garrard.Mcp.Explorer.Core.Domain.Chat;
using Garrard.Mcp.Explorer.Core.Domain.LlmModels;
using Garrard.Mcp.Explorer.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenAI;
using OpenAI.Responses;
using System.ClientModel;
using System.ClientModel.Primitives;

#pragma warning disable OPENAI001

namespace Garrard.Mcp.Explorer.Infrastructure.LlmProviders;

/// <summary>
/// Invokes versioned agents hosted by a Microsoft Foundry project.
/// Agent instructions and tools are owned by the Foundry agent definition.
/// </summary>
public sealed class FoundryProjectAgentService : IFoundryProjectAgentService
{
    private const string TestPrompt = "Reply with OK.";
    private const string AgentApiVersion = "v1";
    private const string FoundryTokenScope = "https://ai.azure.com/.default";
    private const int MaxDiscoveryConcurrency = 4;
    private const int MaxDiscoveryPages = 100;
    private const int MaxToolRounds = 8;

    private readonly IConnectionService? _connectionService;
    private readonly IFoundryToolApprovalService? _approvalService;
    private readonly IHttpClientFactory? _httpClientFactory;
    private readonly ILogger<FoundryProjectAgentService> _logger;

    public FoundryProjectAgentService(
        IConnectionService? connectionService = null,
        ILogger<FoundryProjectAgentService>? logger = null,
        IFoundryToolApprovalService? approvalService = null,
        IHttpClientFactory? httpClientFactory = null)
    {
        _connectionService = connectionService;
        _approvalService = approvalService;
        _httpClientFactory = httpClientFactory;
        _logger = logger ?? NullLogger<FoundryProjectAgentService>.Instance;
    }

    public async Task<IReadOnlyList<FoundryAgentCatalogItem>> DiscoverAgentsAsync(
        LlmModelDefinition model,
        CancellationToken cancellationToken = default)
    {
        ValidateProjectConnection(model);

        if (_httpClientFactory is null)
            throw new InvalidOperationException("Foundry agent discovery is unavailable in this runtime.");

        var authentication = await CreateDiscoveryAuthenticationAsync(model, cancellationToken)
            .ConfigureAwait(false);
        var client = _httpClientFactory.CreateClient("FoundryProjectAgentDiscovery");
        var endpoint = new Uri(model.Endpoint.TrimEnd('/'));
        var agentDocuments = await GetPagedDataAsync(
                client,
                new Uri($"{endpoint.AbsoluteUri}/agents?api-version={AgentApiVersion}&limit=100&order=desc"),
                authentication,
                cancellationToken)
            .ConfigureAwait(false);

        using var discoveryGate = new SemaphoreSlim(MaxDiscoveryConcurrency);
        var agentTasks = agentDocuments
            .Select(async agent =>
            {
                await discoveryGate.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    return await DiscoverAgentAsync(
                            client,
                            endpoint,
                            agent,
                            authentication,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
                finally
                {
                    discoveryGate.Release();
                }
            })
            .ToArray();
        var agents = await Task.WhenAll(agentTasks).ConfigureAwait(false);

        return agents
            .Where(agent => agent is not null)
            .Cast<FoundryAgentCatalogItem>()
            .OrderBy(agent => agent.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async IAsyncEnumerable<ChatStreamEvent> StreamAsync(
        string message,
        IReadOnlyList<CoreChatMessage> history,
        LlmModelDefinition model,
        IReadOnlyList<string> connectionNames,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Validate(model);

        var client = CreateResponsesClient(model);
        var previousResponseId = FindPreviousResponseId(history, model);
        var pendingInputItems = previousResponseId is null
            ? BuildInputItems(history, message)
            : [ResponseItem.CreateUserMessageItem(message)];
        var toolBindings = GetMcpToolBindings(connectionNames);

        _logger.LogInformation(
            "Streaming Foundry project agent {AgentName} using {AgentInvocationMode} with {ToolCount} local MCP execution bindings",
            model.AgentName,
            model.AgentInvocationMode,
            toolBindings.Count);

        for (var round = 1; round <= MaxToolRounds; round++)
        {
            var options = new CreateResponseOptions
            {
                PreviousResponseId = previousResponseId
            };
            foreach (var inputItem in pendingInputItems)
                options.InputItems.Add(inputItem);

            var roundOutputItems = new List<ResponseItem>();
            string? roundResponseId = null;
            var responseFailed = false;

            await foreach (var update in client.CreateResponseStreamingAsync(
                               options,
                               cancellationToken).ConfigureAwait(false))
            {
                if (update is StreamingResponseOutputTextDeltaUpdate textUpdate &&
                    !string.IsNullOrEmpty(textUpdate.Delta))
                {
                    yield return new ChatStreamEvent(ChatStreamEventType.Token) { Text = textUpdate.Delta };
                }
                else if (update is StreamingResponseOutputItemDoneUpdate itemDoneUpdate &&
                         itemDoneUpdate.Item is not null)
                {
                    roundOutputItems.Add(itemDoneUpdate.Item);
                }
                else if (update is StreamingResponseCompletedUpdate completedUpdate &&
                         completedUpdate.Response is { } completedResponse)
                {
                    roundResponseId = completedResponse.Id;
                    if (completedResponse.OutputItems.Count > 0)
                    {
                        roundOutputItems.Clear();
                        roundOutputItems.AddRange(completedResponse.OutputItems);
                    }
                    if (completedResponse.Usage is { } usage)
                    {
                        yield return new ChatStreamEvent(ChatStreamEventType.Usage)
                        {
                            Usage = new ChatTokenUsage(
                                usage.InputTokenCount,
                                usage.OutputTokenCount,
                                usage.TotalTokenCount)
                        };
                    }
                }
                else if (update is StreamingResponseErrorUpdate errorUpdate)
                {
                    yield return new ChatStreamEvent(ChatStreamEventType.Error)
                    {
                        ErrorMessage = errorUpdate.Message
                    };
                    responseFailed = true;
                }
                else if (update is StreamingResponseFailedUpdate failedUpdate)
                {
                    yield return new ChatStreamEvent(ChatStreamEventType.Error)
                    {
                        ErrorMessage = failedUpdate.Response?.Error?.Message ?? "Foundry agent response failed."
                    };
                    responseFailed = true;
                }
            }

            if (responseFailed)
                yield break;

            foreach (var managedMcpEvent in CreateManagedMcpEvents(roundOutputItems))
                yield return managedMcpEvent;

            var approvalRequests = roundOutputItems.OfType<McpToolCallApprovalRequestItem>().ToList();
            if (approvalRequests.Count > 0)
            {
                if (_approvalService is null)
                {
                    yield return new ChatStreamEvent(ChatStreamEventType.Error)
                    {
                        ErrorMessage = "Foundry requested MCP tool approval, but approval handling is unavailable."
                    };
                    yield break;
                }

                if (string.IsNullOrWhiteSpace(roundResponseId))
                {
                    yield return new ChatStreamEvent(ChatStreamEventType.Error)
                    {
                        ErrorMessage = "Foundry did not return a response ID required to continue tool approval."
                    };
                    yield break;
                }

                var approvalResponses = new List<ResponseItem>();
                foreach (var approvalRequest in approvalRequests)
                {
                    var decisionTask = _approvalService.WaitForDecisionAsync(
                        approvalRequest.Id,
                        cancellationToken);

                    yield return new ChatStreamEvent(ChatStreamEventType.ApprovalRequest)
                    {
                        ApprovalRequestId = approvalRequest.Id,
                        ServerLabel = approvalRequest.ServerLabel,
                        ToolName = approvalRequest.ToolName,
                        ToolParameters = approvalRequest.ToolArguments.ToString()
                    };

                    var decision = await decisionTask.ConfigureAwait(false);
                    approvalResponses.Add(ResponseItem.CreateMcpApprovalResponseItem(
                        approvalRequest.Id,
                        decision.Approved));
                }

                previousResponseId = roundResponseId;
                pendingInputItems = approvalResponses;
                continue;
            }

            var functionCalls = roundOutputItems.OfType<FunctionCallResponseItem>().ToList();

            if (functionCalls.Count == 0)
            {
                yield return new ChatStreamEvent(ChatStreamEventType.Done)
                {
                    ProviderResponseId = roundResponseId
                };
                yield break;
            }

            if (string.IsNullOrWhiteSpace(roundResponseId))
            {
                yield return new ChatStreamEvent(ChatStreamEventType.Error)
                {
                    ErrorMessage = "Foundry did not return a response ID required to submit tool output."
                };
                yield break;
            }

            var functionOutputs = new List<ResponseItem>();
            foreach (var functionCall in functionCalls)
            {
                if (!toolBindings.TryGetValue(functionCall.FunctionName, out var binding))
                {
                    yield return new ChatStreamEvent(ChatStreamEventType.Error)
                    {
                        ErrorMessage =
                            $"Foundry agent requested function '{functionCall.FunctionName}', but no selected MCP connection provides it."
                    };
                    yield break;
                }

                var parameters = functionCall.FunctionArguments.ToString();
                yield return new ChatStreamEvent(ChatStreamEventType.ToolCall)
                {
                    ToolName = functionCall.FunctionName,
                    ConnectionName = binding.ConnectionName,
                    ToolParameters = parameters
                };

                var invocation = await InvokeMcpToolAsync(
                        binding.ConnectionName,
                        functionCall.FunctionName,
                        parameters,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (invocation.Exception is not null)
                {
                    _logger.LogWarning(
                        invocation.Exception,
                        "MCP tool {ToolName} on connection {ConnectionName} failed",
                        functionCall.FunctionName,
                        binding.ConnectionName);
                }

                yield return new ChatStreamEvent(ChatStreamEventType.ToolResult)
                {
                    ToolName = functionCall.FunctionName,
                    ConnectionName = binding.ConnectionName,
                    ToolResult = invocation.Output
                };

                functionOutputs.Add(ResponseItem.CreateFunctionCallOutputItem(
                    functionCall.CallId,
                    invocation.Output));
            }

            previousResponseId = roundResponseId;
            pendingInputItems = functionOutputs;
        }

        yield return new ChatStreamEvent(ChatStreamEventType.Error)
        {
            ErrorMessage = $"Foundry agent exceeded the limit of {MaxToolRounds} consecutive tool-call rounds."
        };
    }

    public async Task<string> TestAsync(
        LlmModelDefinition model,
        CancellationToken cancellationToken = default)
    {
        Validate(model);

        var client = CreateResponsesClient(model);
        var options = new CreateResponseOptions();
        options.InputItems.Add(ResponseItem.CreateUserMessageItem(TestPrompt));
        var response = await client.CreateResponseAsync(options, cancellationToken)
            .ConfigureAwait(false);

        return response.Value.GetOutputText() ?? "Agent returned no text output.";
    }

    internal static void Validate(LlmModelDefinition model)
    {
        ValidateProjectConnection(model);

        if (string.IsNullOrWhiteSpace(model.AgentName))
            throw new InvalidOperationException("Foundry agent name is required.");

        if (model.AgentInvocationMode == FoundryAgentInvocationMode.VersionedAgent &&
            string.IsNullOrWhiteSpace(model.AgentVersion))
        {
            throw new InvalidOperationException("Foundry agent version is required for versioned agent invocation.");
        }
    }

    internal static void ValidateProjectConnection(LlmModelDefinition model)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (!Uri.TryCreate(model.Endpoint, UriKind.Absolute, out var endpoint) ||
            endpoint.Scheme != Uri.UriSchemeHttps ||
            !endpoint.AbsolutePath.Contains("/api/projects/", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "A valid HTTPS Foundry project endpoint containing '/api/projects/{project-name}' is required.");
        }

        if (model.AuthenticationMode == LlmAuthenticationMode.ApiKey &&
            string.IsNullOrWhiteSpace(model.ApiKey))
        {
            throw new InvalidOperationException("Foundry project API key is required for API key authentication.");
        }
    }

    private static async Task<DiscoveryAuthentication> CreateDiscoveryAuthenticationAsync(
        LlmModelDefinition model,
        CancellationToken cancellationToken)
    {
        if (model.AuthenticationMode == LlmAuthenticationMode.ApiKey)
            return new DiscoveryAuthentication(null, model.ApiKey);

        var credential = new DefaultAzureCredential();
        var token = await credential.GetTokenAsync(
                new TokenRequestContext([FoundryTokenScope]),
                cancellationToken)
            .ConfigureAwait(false);
        return new DiscoveryAuthentication(token.Token, null);
    }

    private static async Task<FoundryAgentCatalogItem?> DiscoverAgentAsync(
        HttpClient client,
        Uri endpoint,
        JsonElement agentDocument,
        DiscoveryAuthentication authentication,
        CancellationToken cancellationToken)
    {
        if (!agentDocument.TryGetProperty("name", out var nameProperty) ||
            string.IsNullOrWhiteSpace(nameProperty.GetString()))
        {
            return null;
        }

        var name = nameProperty.GetString()!;
        var encodedName = Uri.EscapeDataString(name);
        var versionDocuments = await GetPagedDataAsync(
                client,
                new Uri($"{endpoint.AbsoluteUri}/agents/{encodedName}/versions?api-version={AgentApiVersion}&limit=100&order=desc"),
                authentication,
                cancellationToken)
            .ConfigureAwait(false);

        var versions = versionDocuments
            .Select(MapAgentVersion)
            .Where(version => version is not null)
            .Cast<FoundryAgentVersionCatalogItem>()
            .OrderByDescending(version => version.CreatedAt)
            .ThenByDescending(version => version.Version, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new FoundryAgentCatalogItem(name, versions);
    }

    private static FoundryAgentVersionCatalogItem? MapAgentVersion(JsonElement document)
    {
        if (!document.TryGetProperty("version", out var versionProperty) ||
            string.IsNullOrWhiteSpace(versionProperty.GetString()))
        {
            return null;
        }

        var systemPrompt = string.Empty;
        if (document.TryGetProperty("definition", out var definition) &&
            definition.ValueKind == JsonValueKind.Object &&
            definition.TryGetProperty("instructions", out var instructions) &&
            instructions.ValueKind == JsonValueKind.String)
        {
            systemPrompt = instructions.GetString() ?? string.Empty;
        }

        var description = document.TryGetProperty("description", out var descriptionProperty) &&
                          descriptionProperty.ValueKind == JsonValueKind.String
            ? descriptionProperty.GetString() ?? string.Empty
            : string.Empty;
        var createdAt = document.TryGetProperty("created_at", out var createdAtProperty) &&
                        createdAtProperty.TryGetInt64(out var unixTime)
            ? DateTimeOffset.FromUnixTimeSeconds(unixTime)
            : DateTimeOffset.MinValue;

        return new FoundryAgentVersionCatalogItem(
            versionProperty.GetString()!,
            systemPrompt,
            description,
            createdAt);
    }

    private static async Task<IReadOnlyList<JsonElement>> GetPagedDataAsync(
        HttpClient client,
        Uri firstPage,
        DiscoveryAuthentication authentication,
        CancellationToken cancellationToken)
    {
        var results = new List<JsonElement>();
        var continuationIds = new HashSet<string>(StringComparer.Ordinal);
        Uri? pageUri = firstPage;
        var pageCount = 0;

        while (pageUri is not null)
        {
            if (++pageCount > MaxDiscoveryPages)
                throw new InvalidOperationException("Foundry agent discovery exceeded the pagination safety limit.");

            using var request = new HttpRequestMessage(HttpMethod.Get, pageUri);
            if (authentication.BearerToken is not null)
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authentication.BearerToken);
            else
                request.Headers.TryAddWithoutValidation("api-key", authentication.ApiKey);

            using var response = await client.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);
            using var payload = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            if (!payload.RootElement.TryGetProperty("data", out var data) ||
                data.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidOperationException("Foundry agent discovery returned an invalid list response.");
            }

            results.AddRange(data.EnumerateArray().Select(item => item.Clone()));

            var hasMore = payload.RootElement.TryGetProperty("has_more", out var hasMoreProperty) &&
                          hasMoreProperty.ValueKind is JsonValueKind.True;
            if (!hasMore)
                break;

            if (!payload.RootElement.TryGetProperty("last_id", out var lastIdProperty) ||
                string.IsNullOrWhiteSpace(lastIdProperty.GetString()))
            {
                throw new InvalidOperationException("Foundry agent discovery pagination did not return a continuation ID.");
            }

            var lastId = lastIdProperty.GetString()!;
            if (!continuationIds.Add(lastId))
                throw new InvalidOperationException("Foundry agent discovery returned a repeated continuation ID.");

            pageUri = AddAfterCursor(firstPage, lastId);
        }

        return results;
    }

    private static Uri AddAfterCursor(Uri uri, string after)
    {
        var separator = string.IsNullOrEmpty(uri.Query) ? "?" : "&";
        return new Uri($"{uri.AbsoluteUri}{separator}after={Uri.EscapeDataString(after)}");
    }

    private static ResponsesClient CreateResponsesClient(LlmModelDefinition model)
    {
        var projectEndpoint = new Uri(model.Endpoint.TrimEnd('/'));

        if (model.AuthenticationMode == LlmAuthenticationMode.DefaultAzureCredential)
        {
            var projectClient = new AIProjectClient(projectEndpoint, new DefaultAzureCredential());
            return CreateResponsesClient(projectClient.ProjectOpenAIClient, model);
        }

        if (model.AgentInvocationMode == FoundryAgentInvocationMode.HostedAgentEndpoint)
            return CreateHostedApiKeyResponsesClient(model);

        var apiKeyPolicy = ApiKeyAuthenticationPolicy.CreateHeaderApiKeyPolicy(
            new ApiKeyCredential(model.ApiKey),
            "api-key");
        var options = new ProjectOpenAIClientOptions
        {
            Endpoint = new Uri($"{projectEndpoint.AbsoluteUri.TrimEnd('/')}/openai/v1")
        };
        var projectOpenAiClient = new ProjectOpenAIClient(apiKeyPolicy, options);
        return CreateResponsesClient(projectOpenAiClient, model);
    }

    internal static ResponsesClient CreateResponsesClient(
        ProjectOpenAIClient projectOpenAiClient,
        LlmModelDefinition model)
    {
        var agentName = model.AgentName.Trim();
        if (model.AgentInvocationMode == FoundryAgentInvocationMode.HostedAgentEndpoint)
            return projectOpenAiClient.GetProjectResponsesClientForAgentEndpoint(agentName);

        var agent = new AgentReference(agentName, model.AgentVersion.Trim());
        return projectOpenAiClient.GetProjectResponsesClientForAgent(agent);
    }

    internal static ResponsesClient CreateHostedApiKeyResponsesClient(
        LlmModelDefinition model,
        OpenAIClientOptions? options = null)
    {
        var projectEndpoint = new Uri(model.Endpoint.TrimEnd('/'));
        var encodedAgentName = Uri.EscapeDataString(model.AgentName.Trim());
        options ??= new OpenAIClientOptions();
        options.Endpoint = new Uri(
            $"{projectEndpoint.AbsoluteUri.TrimEnd('/')}/agents/{encodedAgentName}/endpoint/protocols/openai");

        var apiKeyPolicy = ApiKeyAuthenticationPolicy.CreateHeaderApiKeyPolicy(
            new ApiKeyCredential(model.ApiKey),
            "api-key");
        return new ResponsesClient(apiKeyPolicy, options);
    }

    private Dictionary<string, McpToolBinding> GetMcpToolBindings(IReadOnlyList<string> connectionNames)
    {
        if (connectionNames.Count == 0)
            return new Dictionary<string, McpToolBinding>(StringComparer.Ordinal);

        if (_connectionService is null)
            throw new InvalidOperationException("MCP connections are unavailable in this runtime.");

        var bindings = new Dictionary<string, McpToolBinding>(StringComparer.Ordinal);
        foreach (var connectionName in connectionNames.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var connection = _connectionService.GetActiveConnections().FirstOrDefault(c =>
                string.Equals(c.Name, connectionName, StringComparison.OrdinalIgnoreCase));
            if (connection is null)
            {
                _logger.LogWarning("Selected MCP connection {ConnectionName} is not currently connected", connectionName);
                continue;
            }

            foreach (var tool in connection.Tools)
            {
                if (!bindings.TryAdd(tool.Name, new McpToolBinding(connectionName)))
                {
                    throw new InvalidOperationException(
                        $"Selected MCP connections expose duplicate tool name '{tool.Name}'. " +
                        "Select only one of those connections or rename one of the tools.");
                }
            }
        }

        return bindings;
    }

    internal async Task<ToolInvocationResult> InvokeMcpToolAsync(
        string connectionName,
        string toolName,
        string argumentsJson,
        CancellationToken cancellationToken)
    {
        if (_connectionService is null)
            throw new InvalidOperationException("MCP connections are unavailable in this runtime.");

        try
        {
            var arguments = ParseFunctionArguments(argumentsJson);
            var result = await _connectionService.InvokeToolAsync(
                    connectionName,
                    toolName,
                    arguments,
                    cancellationToken)
                .ConfigureAwait(false);
            return new ToolInvocationResult(result, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new ToolInvocationResult(
                JsonSerializer.Serialize(new { isError = true, error = ex.Message }),
                ex);
        }
    }

    internal static Dictionary<string, object?> ParseFunctionArguments(string argumentsJson)
    {
        using var document = JsonDocument.Parse(argumentsJson);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            throw new JsonException("Function arguments must be a JSON object.");

        var arguments = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var property in document.RootElement.EnumerateObject())
            arguments[property.Name] = property.Value.Clone();

        return arguments;
    }

    internal static string? FindPreviousResponseId(
        IReadOnlyList<CoreChatMessage> history,
        LlmModelDefinition model)
    {
        var lastConversationMessage = history.LastOrDefault(item => item.ToolCallName is null);
        if (lastConversationMessage is null ||
            !lastConversationMessage.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(lastConversationMessage.ModelName, model.Name, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(lastConversationMessage.ProviderResponseId))
        {
            return null;
        }

        return lastConversationMessage.ProviderResponseId;
    }

    internal static IReadOnlyList<ChatStreamEvent> CreateManagedMcpEvents(
        IEnumerable<ResponseItem> outputItems)
    {
        var events = new List<ChatStreamEvent>();
        foreach (var mcpCall in outputItems.OfType<McpToolCallItem>())
        {
            events.Add(new ChatStreamEvent(ChatStreamEventType.ToolCall)
            {
                ToolName = mcpCall.ToolName,
                ConnectionName = mcpCall.ServerLabel,
                ServerLabel = mcpCall.ServerLabel,
                ToolParameters = mcpCall.ToolArguments?.ToString()
            });

            var toolResult = mcpCall.ToolOutput ?? mcpCall.Error?.ToString();
            if (!string.IsNullOrWhiteSpace(toolResult))
            {
                events.Add(new ChatStreamEvent(ChatStreamEventType.ToolResult)
                {
                    ToolName = mcpCall.ToolName,
                    ConnectionName = mcpCall.ServerLabel,
                    ServerLabel = mcpCall.ServerLabel,
                    ToolResult = toolResult
                });
            }

            if (mcpCall.Error is not null)
            {
                events.Add(new ChatStreamEvent(ChatStreamEventType.Error)
                {
                    ErrorMessage = $"Foundry MCP tool '{mcpCall.ToolName}' failed: {mcpCall.Error}"
                });
            }
        }

        return events;
    }

    internal static List<ResponseItem> BuildInputItems(
        IReadOnlyList<CoreChatMessage> history,
        string message)
    {
        var items = new List<ResponseItem>();

        foreach (var historicalMessage in history)
        {
            // These are MCP Explorer UI audit entries, not conversation turns.
            if (historicalMessage.ToolCallName is not null)
                continue;

            if (string.IsNullOrWhiteSpace(historicalMessage.Content))
                continue;

            if (historicalMessage.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase))
            {
                items.Add(ResponseItem.CreateAssistantMessageItem(historicalMessage.Content));
            }
            else if (historicalMessage.Role.Equals("system", StringComparison.OrdinalIgnoreCase))
            {
                items.Add(ResponseItem.CreateSystemMessageItem(historicalMessage.Content));
            }
            else
            {
                items.Add(ResponseItem.CreateUserMessageItem(historicalMessage.Content));
            }
        }

        items.Add(ResponseItem.CreateUserMessageItem(message));
        return items;
    }

    internal sealed record McpToolBinding(string ConnectionName);
    internal sealed record ToolInvocationResult(string Output, Exception? Exception);
    private sealed record DiscoveryAuthentication(string? BearerToken, string? ApiKey);
}
