namespace Garrard.Mcp.Explorer.Core.Domain.Chat;

public sealed class ChatSession
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "New Chat";
    public List<ChatMessage> Messages { get; init; } = [];
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    public DateTime LastActivityUtc { get; set; } = DateTime.UtcNow;
}
