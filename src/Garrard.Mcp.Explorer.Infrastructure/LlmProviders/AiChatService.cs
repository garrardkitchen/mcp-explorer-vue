using System.Runtime.CompilerServices;
using System.Text.Json;
using Azure.AI.OpenAI;
using CoreChatMessage = Garrard.Mcp.Explorer.Core.Domain.Chat.ChatMessage;
using Garrard.Mcp.Explorer.Core.Domain.Chat;
using Garrard.Mcp.Explorer.Core.Domain.LlmModels;
using Garrard.Mcp.Explorer.Core.Interfaces;
using Garrard.Mcp.Explorer.Infrastructure.Mcp;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenAI;

namespace Garrard.Mcp.Explorer.Infrastructure.LlmProviders;

/// <summary>
/// Streams AI chat responses using Microsoft.Extensions.AI with MCP tool calling.
/// Requires <see cref="ConnectionService"/> to access live MCP tool lists.
/// </summary>
public sealed class AiChatService : IAiChatService
{
    private readonly ILogger<AiChatService> _logger;
    private readonly ConnectionService _connectionService;

    public AiChatService(ILogger<AiChatService> logger, ConnectionService connectionService)
    {
        _logger = logger;
        _connectionService = connectionService;
    }

    public async IAsyncEnumerable<ChatStreamEvent> StreamAsync(
        string message,
        List<CoreChatMessage> history,
        LlmModelDefinition model,
        IReadOnlyList<string> connectionNames,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        IChatClient? baseClient = null;
        string? clientError = null;
        try
        {
            baseClient = CreateBaseChatClient(model);
        }
        catch (Exception ex)
        {
            clientError = ex.Message;
        }

        if (clientError is not null)
        {
            yield return new ChatStreamEvent(ChatStreamEventType.Error) { ErrorMessage = clientError };
            yield break;
        }

        var samplingClient = new ChatClientBuilder(baseClient!)
            .Use(next => new LoggingChatClient(next, _logger))
            .Build();

        await _connectionService.AttachSamplingHandlerAsync(samplingClient, cancellationToken).ConfigureAwait(false);

        var client = new ChatClientBuilder(baseClient!)
            .UseFunctionInvocation()
            .Use(next => new LoggingChatClient(next, _logger))
            .Build();

        var messages = BuildMessages(history, message, model);
        var tools = GetMcpTools(connectionNames);

        var chatOptions = new ChatOptions();
        if (tools.Count > 0)
            chatOptions.Tools = tools;

        _logger.LogInformation("Streaming '{Message}' with model {Model}, {ToolCount} tools",
            message, model.ModelName, tools.Count);

        var toolToConnection = BuildToolConnectionMap(connectionNames);
        var announcedCalls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        ChatTokenUsage? latestUsage = null;
        var pendingEvents = new List<ChatStreamEvent>();

        await foreach (var update in client.GetStreamingResponseAsync(messages, chatOptions, cancellationToken).ConfigureAwait(false))
        {
            pendingEvents.Clear();

            // Extract usage (best-effort, no yields inside try/catch)
            try
            {
                var usage = TryExtractUsage(update);
                if (usage is not null)
                {
                    latestUsage = usage;
                    pendingEvents.Add(new ChatStreamEvent(ChatStreamEventType.Usage) { Usage = usage });
                }

                if (update.Contents is not null)
                {
                    foreach (var c in update.Contents)
                    {
                        if (c is UsageContent uc)
                        {
                            var mapped = MapUsageObject(uc.Details);
                            if (mapped is not null)
                            {
                                latestUsage = mapped;
                                pendingEvents.Add(new ChatStreamEvent(ChatStreamEventType.Usage) { Usage = mapped });
                            }
                        }
                    }
                }
            }
            catch { /* best-effort */ }

            foreach (var e in pendingEvents)
                yield return e;

            // Tool calls
            if (update.Contents is not null)
            {
                foreach (var c in update.Contents)
                {
                    if (c is FunctionCallContent fcc)
                    {
                        var key = string.IsNullOrEmpty(fcc.CallId) ? fcc.Name : $"{fcc.Name}:{fcc.CallId}";
                        if (announcedCalls.Add(key))
                        {
                            var connectionName = toolToConnection.TryGetValue(fcc.Name, out var cn) ? cn : string.Empty;
                            string? parametersJson = null;
                            try
                            {
                                if (fcc.Arguments is not null)
                                    parametersJson = JsonSerializer.Serialize(fcc.Arguments, new JsonSerializerOptions { WriteIndented = false });
                            }
                            catch (Exception ex) { _logger.LogWarning(ex, "Failed to serialize args for {Tool}", fcc.Name); }

                            yield return new ChatStreamEvent(ChatStreamEventType.ToolCall)
                            {
                                ToolName = fcc.Name,
                                ConnectionName = connectionName,
                                ToolParameters = parametersJson
                            };
                        }
                    }
                }
            }

            if (!string.IsNullOrEmpty(update.Text))
                yield return new ChatStreamEvent(ChatStreamEventType.Token) { Text = update.Text };
        }

        yield return new ChatStreamEvent(ChatStreamEventType.Done);
    }

    private IChatClient CreateBaseChatClient(LlmModelDefinition model) => model.ProviderType switch
    {
        "OpenAI" => CreateOpenAiClient(model),
        "AzureAIFoundry" => CreateAzureClient(model),
        _ => throw new NotSupportedException($"Provider type '{model.ProviderType}' is not supported.")
    };

    private IChatClient CreateOpenAiClient(LlmModelDefinition model)
    {
        if (string.IsNullOrWhiteSpace(model.ApiKey))
            throw new InvalidOperationException("OpenAI API key is required.");

        var options = new OpenAIClientOptions();
        if (!string.IsNullOrWhiteSpace(model.Endpoint))
            options.Endpoint = new Uri(model.Endpoint.TrimEnd('/') + "/");

        var sdk = new OpenAIClient(new System.ClientModel.ApiKeyCredential(model.ApiKey), options);
        return sdk.GetChatClient(model.ModelName).AsIChatClient();
    }

    private IChatClient CreateAzureClient(LlmModelDefinition model)
    {
        if (string.IsNullOrWhiteSpace(model.Endpoint))
            throw new InvalidOperationException("Azure OpenAI endpoint is required.");
        if (string.IsNullOrWhiteSpace(model.ApiKey))
            throw new InvalidOperationException("Azure OpenAI API key is required.");

        var azClient = new AzureOpenAIClient(new Uri(model.Endpoint), new System.ClientModel.ApiKeyCredential(model.ApiKey));
        return azClient.GetChatClient(model.DeploymentName).AsIChatClient();
    }

    private static List<Microsoft.Extensions.AI.ChatMessage> BuildMessages(
        List<CoreChatMessage> history, string newMessage, LlmModelDefinition model)
    {
        var messages = new List<Microsoft.Extensions.AI.ChatMessage>();

        if (!string.IsNullOrWhiteSpace(model.SystemPrompt))
            messages.Add(new Microsoft.Extensions.AI.ChatMessage(ChatRole.System, model.SystemPrompt));

        foreach (var m in history)
        {
            // Skip tool-call tracking messages — they are UI artifacts, not conversation context.
            if (m.Role.Equals("System", StringComparison.OrdinalIgnoreCase) && m.ToolCallName is not null)
                continue;

            var role = m.Role.ToLowerInvariant() switch
            {
                "assistant" => ChatRole.Assistant,
                "system" => ChatRole.System,
                _ => ChatRole.User
            };
            messages.Add(new Microsoft.Extensions.AI.ChatMessage(role, m.Content));
        }

        messages.Add(new Microsoft.Extensions.AI.ChatMessage(ChatRole.User, newMessage));
        return messages;
    }

    private List<AITool> GetMcpTools(IReadOnlyList<string> connectionNames)
    {
        var tools = new List<AITool>();
        foreach (var name in connectionNames)
        {
            var ctx = _connectionService.GetConnectionContext(name);
            if (ctx is null) continue;
            foreach (var tool in ctx.Tools)
                tools.Add(tool);
        }
        return tools;
    }

    private Dictionary<string, string> BuildToolConnectionMap(IReadOnlyList<string> connectionNames)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in connectionNames)
        {
            var ctx = _connectionService.GetConnectionContext(name);
            if (ctx is null) continue;
            foreach (var t in ctx.Tools)
                map.TryAdd(t.Name, name);
        }
        return map;
    }

    private static Core.Domain.Chat.ChatTokenUsage? TryExtractUsage(object update)
    {
        if (update is null) return null;
        var usageProp = update.GetType().GetProperty("Usage");
        if (usageProp?.GetValue(update) is { } usageObj)
        {
            var mapped = MapUsageObject(usageObj);
            if (mapped is not null) return mapped;
        }
        var input = TryGetInt(update, "InputTokenCount", "PromptTokens", "PromptTokenCount", "InputTokens");
        var output = TryGetInt(update, "OutputTokenCount", "CompletionTokens", "CompletionTokenCount", "OutputTokens");
        var total = TryGetInt(update, "TotalTokenCount", "TotalTokens");
        if (input.HasValue || output.HasValue || total.HasValue)
            return new Core.Domain.Chat.ChatTokenUsage(input ?? 0, output ?? 0, total ?? (input ?? 0) + (output ?? 0));
        return null;
    }

    private static Core.Domain.Chat.ChatTokenUsage? MapUsageObject(object? usageObj)
    {
        if (usageObj is null) return null;
        var input = TryGetInt(usageObj, "InputTokenCount", "PromptTokens", "PromptTokenCount", "InputTokens");
        var output = TryGetInt(usageObj, "OutputTokenCount", "CompletionTokens", "CompletionTokenCount", "OutputTokens");
        var total = TryGetInt(usageObj, "TotalTokenCount", "TotalTokens");
        if (input.HasValue || output.HasValue || total.HasValue)
            return new Core.Domain.Chat.ChatTokenUsage(input ?? 0, output ?? 0, total ?? (input ?? 0) + (output ?? 0));
        return null;
    }

    private static int? TryGetInt(object obj, params string[] names)
    {
        foreach (var name in names)
        {
            var p = obj.GetType().GetProperty(name);
            if (p is null) continue;
            try
            {
                var val = p.GetValue(obj);
                if (val is null) continue;
                if (val is int i) return i;
                if (val is long l) return checked((int)l);
                if (val is string s && int.TryParse(s, out var si)) return si;
            }
            catch { }
        }
        return null;
    }

    private sealed class LoggingChatClient(IChatClient innerClient, ILogger logger) : DelegatingChatClient(innerClient)
    {
        public override async Task<ChatResponse> GetResponseAsync(
            IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            logger.LogDebug("GetResponseAsync called with {Count} messages", messages.Count());
            return await base.GetResponseAsync(messages, options, cancellationToken).ConfigureAwait(false);
        }

        public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            logger.LogDebug("GetStreamingResponseAsync called");
            await foreach (var update in base.GetStreamingResponseAsync(messages, options, cancellationToken).ConfigureAwait(false))
                yield return update;
        }
    }
}
