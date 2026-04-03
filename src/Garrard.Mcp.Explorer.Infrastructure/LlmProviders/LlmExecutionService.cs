using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Garrard.Mcp.Explorer.Core.Domain.LlmModels;
using Garrard.Mcp.Explorer.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Garrard.Mcp.Explorer.Infrastructure.LlmProviders;

/// <summary>
/// Direct HTTP-based LLM prompt execution (non-streaming, one-shot).
/// Supports OpenAI and Azure AI Foundry providers.
/// </summary>
public sealed class LlmExecutionService : ILlmExecutionService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<LlmExecutionService> _logger;
    private readonly string _azureApiVersion;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public LlmExecutionService(
        IHttpClientFactory httpClientFactory,
        ILogger<LlmExecutionService> logger,
        IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _azureApiVersion = configuration["LlmService:AzureApiVersion"] ?? "2024-02-15-preview";
    }

    public async Task<string> ExecutePromptAsync(
        LlmModelDefinition model,
        string userMessage,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(userMessage);

        _logger.LogInformation("Executing prompt with provider: {Provider}, Model: {Model}",
            model.ProviderType, model.ModelName);

        return model.ProviderType.ToUpperInvariant() switch
        {
            "OPENAI" => await ExecuteOpenAiAsync(model, userMessage, cancellationToken).ConfigureAwait(false),
            "AZUREAIFOUNDRY" => await ExecuteAzureAsync(model, userMessage, cancellationToken).ConfigureAwait(false),
            _ => throw new NotSupportedException($"Provider '{model.ProviderType}' is not supported.")
        };
    }

    private async Task<string> ExecuteOpenAiAsync(LlmModelDefinition model, string userMessage, CancellationToken ct)
    {
        using var http = _httpClientFactory.CreateClient("OpenAI");
        using var req = new HttpRequestMessage(HttpMethod.Post, "chat/completions");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", model.ApiKey);

        var body = new ChatRequest
        {
            Model = model.ModelName,
            Messages = [new ChatMsg("system", model.SystemPrompt), new ChatMsg("user", userMessage)],
            Temperature = 0.7
        };

        req.Content = JsonContent.Create(body, options: JsonOptions);

        using var resp = await http.SendAsync(req, ct).ConfigureAwait(false);
        await EnsureSuccessAsync(resp, "OpenAI", ct).ConfigureAwait(false);

        var result = await resp.Content.ReadFromJsonAsync<ChatResponse>(JsonOptions, ct).ConfigureAwait(false);
        var content = result?.Choices?.FirstOrDefault()?.Message?.Content ?? "No response from OpenAI.";

        _logger.LogInformation("OpenAI completed. Tokens: {T}", result?.Usage?.TotalTokens ?? 0);
        return content;
    }

    private async Task<string> ExecuteAzureAsync(LlmModelDefinition model, string userMessage, CancellationToken ct)
    {
        using var http = _httpClientFactory.CreateClient("AzureAIFoundry");
        var deployment = string.IsNullOrWhiteSpace(model.DeploymentName) ? model.ModelName : model.DeploymentName;
        var url = $"{model.Endpoint.TrimEnd('/')}/openai/deployments/{deployment}/chat/completions?api-version={_azureApiVersion}";

        var first = await SendAzureAsync(http, url, model.ApiKey,
            [new ChatMsg("system", model.SystemPrompt), new ChatMsg("user", userMessage)], ct).ConfigureAwait(false);

        if (string.IsNullOrEmpty(first))
            return "No response from Azure AI Foundry.";

        var second = await SendAzureAsync(http, url, model.ApiKey,
            [new ChatMsg("system", model.SystemPrompt), new ChatMsg("assistant", first)], ct).ConfigureAwait(false);

        static string Trunc(string? s, int max) =>
            string.IsNullOrEmpty(s) ? string.Empty : (s.Length <= max ? s : s[..max] + "…");

        return $"**Prompt**: {Trunc(first, 2000)}\n\n**Response**: {Trunc(second, 2000)}";
    }

    private async Task<string> SendAzureAsync(HttpClient http, string url, string apiKey, ChatMsg[] messages, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Add("api-key", apiKey);
        req.Content = JsonContent.Create(new ChatRequest { Messages = messages, Temperature = 0.7, MaxTokens = 4096 }, options: JsonOptions);

        using var resp = await http.SendAsync(req, ct).ConfigureAwait(false);
        await EnsureSuccessAsync(resp, "Azure AI Foundry", ct).ConfigureAwait(false);

        var result = await resp.Content.ReadFromJsonAsync<ChatResponse>(JsonOptions, ct).ConfigureAwait(false);
        _logger.LogInformation("Azure AI Foundry completed. Tokens: {T}", result?.Usage?.TotalTokens ?? 0);
        return result?.Choices?.FirstOrDefault()?.Message?.Content ?? string.Empty;
    }

    private async Task EnsureSuccessAsync(HttpResponseMessage resp, string provider, CancellationToken ct)
    {
        if (!resp.IsSuccessStatusCode)
        {
            var err = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (err.Length > 2048) err = err[..2048] + "…";
            _logger.LogWarning("{Provider} error {Status}: {Error}", provider, resp.StatusCode, err);
            resp.EnsureSuccessStatusCode();
        }
    }

    private sealed class ChatRequest
    {
        [JsonPropertyName("model")] public string? Model { get; init; }
        [JsonPropertyName("messages")] public required ChatMsg[] Messages { get; init; }
        [JsonPropertyName("temperature")] public double Temperature { get; init; } = 0.7;
        [JsonPropertyName("max_tokens")] public int? MaxTokens { get; init; }
    }

    private sealed class ChatMsg(string role, string content)
    {
        [JsonPropertyName("role")] public string Role { get; } = role;
        [JsonPropertyName("content")] public string Content { get; } = content;
    }

    private sealed class ChatResponse
    {
        [JsonPropertyName("choices")] public ChatChoice[]? Choices { get; init; }
        [JsonPropertyName("usage")] public TokenUsage? Usage { get; init; }
    }

    private sealed class ChatChoice
    {
        [JsonPropertyName("message")] public ChatResponseMsg? Message { get; init; }
    }

    private sealed class ChatResponseMsg
    {
        [JsonPropertyName("content")] public string? Content { get; init; }
    }

    private sealed class TokenUsage
    {
        [JsonPropertyName("total_tokens")] public int TotalTokens { get; init; }
    }
}
