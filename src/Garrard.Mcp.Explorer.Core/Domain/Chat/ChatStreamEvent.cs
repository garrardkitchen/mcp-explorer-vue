namespace Garrard.Mcp.Explorer.Core.Domain.Chat;

public enum ChatStreamEventType { Token, ToolCall, ToolResult, Usage, Done, Error }

public sealed record ChatStreamEvent(ChatStreamEventType Type)
{
    public string? Text { get; init; }
    public string? ToolName { get; init; }
    public string? ToolParameters { get; init; }
    public string? ToolResult { get; init; }
    public string? ConnectionName { get; init; }
    public ChatTokenUsage? Usage { get; init; }
    public string? MessageId { get; init; }
    public string? ErrorMessage { get; init; }
}
