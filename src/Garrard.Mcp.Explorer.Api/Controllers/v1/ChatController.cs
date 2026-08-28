using System.Text;
using System.Text.Json;
using Asp.Versioning;
using Garrard.Mcp.Explorer.Core.Domain.Chat;
using Garrard.Mcp.Explorer.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Garrard.Mcp.Explorer.Api.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/chat")]
public sealed class ChatController(
    IAiChatService chatService,
    IUserPreferencesStore preferencesStore,
    ISecretProtector secretProtector,
    IHttpClientFactory httpClientFactory) : ControllerBase
{
    private const long MaxPreviewBytes = 50L * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [HttpPost("document-preview")]
    public async Task<IActionResult> PreviewDocument(
        [FromBody] DocumentPreviewRequest request,
        CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            !uri.Host.EndsWith(".blob.core.windows.net", StringComparison.OrdinalIgnoreCase) ||
            uri.Host.Length <= ".blob.core.windows.net".Length ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !uri.IsDefaultPort)
        {
            return BadRequest(new { error = "Only HTTPS Azure Blob Storage document URLs are supported." });
        }

        try
        {
            var client = httpClientFactory.CreateClient("ChatDocumentPreview");
            using var upstream = await client.GetAsync(
                uri,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if ((int)upstream.StatusCode is >= 300 and < 400)
                return StatusCode(StatusCodes.Status502BadGateway, new { error = "Document redirects are not allowed." });
            if (!upstream.IsSuccessStatusCode)
                return StatusCode(StatusCodes.Status502BadGateway, new { error = "The document source rejected the request." });
            if (upstream.Content.Headers.ContentLength is > MaxPreviewBytes)
                return StatusCode(StatusCodes.Status413PayloadTooLarge, new { error = "The document exceeds the 50 MB preview limit." });

            await using var source = await upstream.Content.ReadAsStreamAsync(cancellationToken);
            await using var buffer = new MemoryStream();
            var chunk = new byte[81920];
            long total = 0;
            while (true)
            {
                var read = await source.ReadAsync(chunk, cancellationToken);
                if (read == 0) break;
                total += read;
                if (total > MaxPreviewBytes)
                    return StatusCode(StatusCodes.Status413PayloadTooLarge, new { error = "The document exceeds the 50 MB preview limit." });
                await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
            }

            var contentType = upstream.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
            return File(buffer.ToArray(), contentType);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { error = "The document source could not be reached." });
        }
        catch (TaskCanceledException)
        {
            return StatusCode(StatusCodes.Status504GatewayTimeout, new { error = "The document source timed out." });
        }
    }

    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessions(CancellationToken cancellationToken)
    {
        var prefs = await preferencesStore.LoadAsync(cancellationToken);
        return Ok(prefs.ChatSessions
            .OrderByDescending(s => s.LastActivityUtc)
            .ToList()
            .Select(s => new
            {
                s.Id,
                s.Name,
                s.CreatedAtUtc,
                s.LastActivityUtc,
                messageCount = s.Messages.Count
            }));
    }

    [HttpPost("sessions")]
    public async Task<IActionResult> CreateSession(CancellationToken cancellationToken)
    {
        var session = new ChatSession { Name = $"Chat {DateTime.UtcNow:yyyy-MM-dd HH:mm}" };
        var prefs = await preferencesStore.LoadAsync(cancellationToken);
        var updated = prefs with { ChatSessions = [..prefs.ChatSessions, session] };
        await preferencesStore.SaveAsync(updated, cancellationToken);
        return CreatedAtAction(nameof(GetSessions), new { }, new { session.Id, session.Name });
    }

    [HttpDelete("sessions/{sessionId}")]
    public async Task<IActionResult> DeleteSession(string sessionId, CancellationToken cancellationToken)
    {
        var prefs = await preferencesStore.LoadAsync(cancellationToken);
        var updated = prefs with { ChatSessions = prefs.ChatSessions.Where(s => s.Id != sessionId).ToList() };
        await preferencesStore.SaveAsync(updated, cancellationToken);
        return NoContent();
    }

    [HttpPatch("sessions/{sessionId}")]
    public async Task<IActionResult> RenameSession(string sessionId, [FromBody] RenameSessionRequest request, CancellationToken cancellationToken)
    {
        var prefs = await preferencesStore.LoadAsync(cancellationToken);
        var session = prefs.ChatSessions.FirstOrDefault(s => s.Id == sessionId);
        if (session is null) return NotFound();
        session.Name = request.Name?.Trim() ?? session.Name;
        await preferencesStore.SaveAsync(prefs, cancellationToken);
        return NoContent();
    }

    [HttpGet("sessions/{sessionId}/messages")]
    public async Task<IActionResult> GetMessages(string sessionId, CancellationToken cancellationToken)
    {
        var prefs = await preferencesStore.LoadAsync(cancellationToken);
        var session = prefs.ChatSessions.FirstOrDefault(s => s.Id == sessionId);
        if (session is null) return NotFound();
        return Ok(session.Messages.Select(m => new
        {
            m.Id,
            m.Role,
            m.Content,
            m.TimestampUtc,
            m.ToolCallName,
            ToolCallParameters = DecryptToolParameters(m.ToolCallParameters),
            m.ToolResult,
            m.ConnectionName,
            m.ModelName,
            m.ProviderResponseId,
            m.TokenUsage,
            m.ThinkingMilliseconds,
            m.SensitiveSegments,
            m.PromptName,
            m.PromptInvocationParams,
        }));
    }

    [HttpPost("sessions/{sessionId}/messages")]
    public async Task StreamMessage(string sessionId, [FromBody] SendMessageRequest request, CancellationToken cancellationToken)
    {
        var prefs = await preferencesStore.LoadAsync(cancellationToken);
        var session = prefs.ChatSessions.FirstOrDefault(s => s.Id == sessionId);
        if (session is null)
        {
            Response.StatusCode = 404;
            return;
        }

        Response.Headers.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        var model = prefs.LlmModels.FirstOrDefault(m => m.Name == request.ModelName)
                    ?? prefs.LlmModels.FirstOrDefault();
        if (model is null)
        {
            await WriteSseEvent("error", new { error = "No LLM model configured." }, cancellationToken);
            return;
        }

        // Snapshot history BEFORE adding the new user message — BuildMessages appends
        // the new message itself, so passing the snapshot avoids sending it twice.
        var historySnapshot = session.Messages.ToList();

        var userMessage = new ChatMessage
        {
            Role = "user",
            Content = request.Message,
            TimestampUtc = DateTime.UtcNow,
            PromptName = request.PromptName,
            PromptInvocationParams = request.PromptInvocationParams,
        };
        session.Messages.Add(userMessage);
        session.LastActivityUtc = DateTime.UtcNow;

        // Stream and accumulate response
        var assistantContent = new System.Text.StringBuilder();
        string? assistantMessageId = null;
        string? providerResponseId = null;
        var toolCallMessages = new List<ChatMessage>();
        var thinkingStart = DateTime.UtcNow;
        int? thinkingMilliseconds = null;
        bool firstToken = false;
        ChatTokenUsage? tokenUsage = null;
        var clientDisconnected = false;

        try
        {
            await foreach (var evt in chatService.StreamAsync(
                request.Message, historySnapshot, model, request.ConnectionNames ?? [], cancellationToken))
            {
                // Convert PascalCase enum to kebab-case: ToolCall → tool-call
                var eventName = evt.Type switch
                {
                    ChatStreamEventType.ToolCall => "tool-call",
                    ChatStreamEventType.ToolResult => "tool-result",
                    ChatStreamEventType.ApprovalRequest => "approval-request",
                    _ => evt.Type.ToString().ToLowerInvariant()
                };
                await WriteSseEvent(eventName, evt, cancellationToken);

                if (evt.Type == ChatStreamEventType.Token && evt.Text is not null)
                {
                    if (!firstToken)
                    {
                        firstToken = true;
                        thinkingMilliseconds = (int)(DateTime.UtcNow - thinkingStart).TotalMilliseconds;
                    }
                    assistantContent.Append(evt.Text);
                }
                else if (evt.Type == ChatStreamEventType.ToolCall)
                {
                    toolCallMessages.Add(new ChatMessage
                    {
                        Role = "System",
                        Content = $"Calling {evt.ToolName}",
                        TimestampUtc = DateTime.UtcNow,
                        ToolCallName = evt.ToolName,
                        ToolCallParameters = evt.ToolParameters,
                        ConnectionName = evt.ConnectionName,
                        ModelName = model.Name,
                    });
                }
                else if (evt.Type == ChatStreamEventType.ToolResult)
                {
                    var toolCall = toolCallMessages.LastOrDefault(item =>
                        item.ToolResult is null &&
                        string.Equals(item.ToolCallName, evt.ToolName, StringComparison.Ordinal) &&
                        string.Equals(item.ConnectionName, evt.ConnectionName, StringComparison.Ordinal));
                    if (toolCall is not null)
                        toolCall.ToolResult = evt.ToolResult;
                }
                else if (evt.Type == ChatStreamEventType.Usage && evt.Usage is not null)
                {
                    tokenUsage = evt.Usage; // Keep the latest usage (final pass has the full totals)
                }
                else if (evt.Type == ChatStreamEventType.Done)
                {
                    if (evt.MessageId is not null)
                        assistantMessageId = evt.MessageId;
                    if (evt.ProviderResponseId is not null)
                        providerResponseId = evt.ProviderResponseId;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Client disconnected — save partial content if any
            clientDisconnected = true;
        }
        catch (Exception ex)
        {
            await WriteSseEvent("error", new { error = ex.Message }, cancellationToken);
        }

        // Persist tool call messages first (in order they arrived)
        foreach (var tc in toolCallMessages)
            session.Messages.Add(tc);

        // Save assistant message with accumulated token usage
        var assistantMessage = new ChatMessage
        {
            Id = assistantMessageId ?? Guid.NewGuid().ToString(),
            Role = "assistant",
            Content = assistantContent.ToString(),
            TimestampUtc = DateTime.UtcNow,
            ModelName = model.Name,
            ProviderResponseId = providerResponseId,
            ThinkingMilliseconds = thinkingMilliseconds,
            TokenUsage = tokenUsage,
        };
        session.Messages.Add(assistantMessage);
        session.LastActivityUtc = DateTime.UtcNow;

        // Persist updated session
        if (clientDisconnected)
        {
            using var persistCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await preferencesStore.SaveAsync(prefs, persistCts.Token);
        }
        else
        {
            await preferencesStore.SaveAsync(prefs, cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
    }

    private string? DecryptToolParameters(string? paramsJson)
    {
        if (string.IsNullOrEmpty(paramsJson)) return paramsJson;
        const string encPrefix = "enc:";
        try
        {
            using var doc = JsonDocument.Parse(paramsJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return paramsJson;

            var result = new Dictionary<string, object?>();
            var anyDecrypted = false;
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.String)
                {
                    var val = prop.Value.GetString()!;
                    if (val.StartsWith(encPrefix, StringComparison.Ordinal))
                    {
                        result[prop.Name] = secretProtector.Decrypt(val);
                        anyDecrypted = true;
                    }
                    else result[prop.Name] = val;
                }
                else
                {
                    result[prop.Name] = prop.Value.Clone();
                }
            }
            return anyDecrypted ? JsonSerializer.Serialize(result, JsonOptions) : paramsJson;
        }
        catch { return paramsJson; }
    }

    private async Task WriteSseEvent(string eventName, object data, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(data, JsonOptions);
        var payload = $"event: {eventName}\ndata: {json}\n\n";
        await Response.Body.WriteAsync(Encoding.UTF8.GetBytes(payload), cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }
}

public sealed record SendMessageRequest(string Message, string? ModelName, IReadOnlyList<string>? ConnectionNames, string? PromptName = null, string? PromptInvocationParams = null);
public sealed record RenameSessionRequest(string? Name);

public sealed record DocumentPreviewRequest(string Url);