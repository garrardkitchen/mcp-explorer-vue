using Garrard.Mcp.Explorer.Core.Domain.Chat;

namespace Garrard.Tests.Mcp.Explorer.Core.Domain.Chat;

public class ChatSessionTests
{
    // ── ChatSession ───────────────────────────────────────────────────────────

    [Fact]
    public void ChatSession_DefaultConstruction_SetsExpectedDefaults()
    {
        var session = new ChatSession();

        Assert.Equal("New Chat", session.Name);
        Assert.NotNull(session.Id);
        Assert.NotEmpty(session.Id);
        Assert.Empty(session.Messages);
    }

    [Fact]
    public void ChatSession_Id_IsUniquePerInstance()
    {
        var s1 = new ChatSession();
        var s2 = new ChatSession();

        Assert.NotEqual(s1.Id, s2.Id);
    }

    [Fact]
    public void ChatSession_CanAddMessages()
    {
        var session = new ChatSession();
        session.Messages.Add(new ChatMessage { Role = "user", Content = "Hello" });
        session.Messages.Add(new ChatMessage { Role = "assistant", Content = "Hi there" });

        Assert.Equal(2, session.Messages.Count);
    }

    [Fact]
    public void ChatSession_NameCanBeChanged()
    {
        var session = new ChatSession { Name = "My Conversation" };

        Assert.Equal("My Conversation", session.Name);
    }

    // ── ChatMessage ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData("user")]
    [InlineData("assistant")]
    [InlineData("tool")]
    public void ChatMessage_RoleProperty_AcceptsExpectedRoles(string role)
    {
        var msg = new ChatMessage { Role = role };

        Assert.Equal(role, msg.Role);
    }

    [Fact]
    public void ChatMessage_DefaultRole_IsUser()
    {
        var msg = new ChatMessage();

        Assert.Equal("user", msg.Role);
    }

    [Fact]
    public void ChatMessage_Id_IsUniquePerInstance()
    {
        var m1 = new ChatMessage();
        var m2 = new ChatMessage();

        Assert.NotEqual(m1.Id, m2.Id);
    }

    [Fact]
    public void ChatMessage_ToolCallProperties_DefaultToNull()
    {
        var msg = new ChatMessage();

        Assert.Null(msg.ToolCallName);
        Assert.Null(msg.ToolCallParameters);
        Assert.Null(msg.ModelName);
        Assert.Null(msg.TokenUsage);
    }

    // ── ChatTokenUsage ────────────────────────────────────────────────────────

    [Fact]
    public void ChatTokenUsage_TotalTokens_ReflectsSuppliedValue()
    {
        var usage = new ChatTokenUsage(InputTokens: 150, OutputTokens: 50, TotalTokens: 200);

        Assert.Equal(150, usage.InputTokens);
        Assert.Equal(50, usage.OutputTokens);
        Assert.Equal(200, usage.TotalTokens);
    }

    [Fact]
    public void ChatTokenUsage_TotalEqualsInputPlusOutput_WhenSetCorrectly()
    {
        const int input = 300;
        const int output = 120;
        var usage = new ChatTokenUsage(input, output, input + output);

        Assert.Equal(usage.InputTokens + usage.OutputTokens, usage.TotalTokens);
    }

    [Fact]
    public void ChatTokenUsage_RecordEquality_WorksCorrectly()
    {
        var u1 = new ChatTokenUsage(10, 20, 30);
        var u2 = new ChatTokenUsage(10, 20, 30);
        var u3 = new ChatTokenUsage(1, 2, 3);

        Assert.Equal(u1, u2);
        Assert.NotEqual(u1, u3);
    }

    // ── ChatStreamEvent ───────────────────────────────────────────────────────

    [Theory]
    [InlineData(ChatStreamEventType.Token)]
    [InlineData(ChatStreamEventType.ToolCall)]
    [InlineData(ChatStreamEventType.ToolResult)]
    [InlineData(ChatStreamEventType.Usage)]
    [InlineData(ChatStreamEventType.Done)]
    [InlineData(ChatStreamEventType.Error)]
    public void ChatStreamEvent_CanBeCreatedForEachType(ChatStreamEventType type)
    {
        var evt = new ChatStreamEvent(type);

        Assert.Equal(type, evt.Type);
    }

    [Fact]
    public void ChatStreamEvent_TokenEvent_CarriesText()
    {
        var evt = new ChatStreamEvent(ChatStreamEventType.Token) { Text = "Hello " };

        Assert.Equal("Hello ", evt.Text);
    }

    [Fact]
    public void ChatStreamEvent_ToolCallEvent_CarriesToolInfo()
    {
        var evt = new ChatStreamEvent(ChatStreamEventType.ToolCall)
        {
            ToolName = "get_weather",
            ToolParameters = """{"city":"London"}""",
            ConnectionName = "my-mcp"
        };

        Assert.Equal("get_weather", evt.ToolName);
        Assert.Equal("""{"city":"London"}""", evt.ToolParameters);
        Assert.Equal("my-mcp", evt.ConnectionName);
    }

    [Fact]
    public void ChatStreamEvent_ErrorEvent_CarriesMessage()
    {
        var evt = new ChatStreamEvent(ChatStreamEventType.Error) { ErrorMessage = "Connection failed" };

        Assert.Equal("Connection failed", evt.ErrorMessage);
    }

    [Fact]
    public void ChatStreamEvent_UsageEvent_CarriesTokenUsage()
    {
        var usage = new ChatTokenUsage(100, 50, 150);
        var evt = new ChatStreamEvent(ChatStreamEventType.Usage) { Usage = usage };

        Assert.Equal(usage, evt.Usage);
    }
}
