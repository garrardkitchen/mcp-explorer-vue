namespace Garrard.Mcp.Explorer.Core.Domain.Chat;

public sealed class ChatMessage
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string Role { get; init; } = "user";
    public string Content { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
    public string? ToolCallName { get; set; }
    public string? ToolCallParameters { get; set; }
    public string? ConnectionName { get; set; }
    public string? ModelName { get; set; }
    public ChatTokenUsage? TokenUsage { get; set; }
    public int? ThinkingMilliseconds { get; set; }
    public List<SensitiveSegment> SensitiveSegments { get; init; } = [];
    // Prompt invocation — set when a user message originated from the prompt picker
    public string? PromptName { get; set; }
    public string? PromptInvocationParams { get; set; }  // JSON string {"key":"value"}
}
