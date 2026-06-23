namespace Garrard.Mcp.Explorer.Cli.Runbooks;

public sealed class RunbookAssertion
{
    public string? Path { get; set; }
    public string Operator { get; set; } = "equals";
    public object? Value { get; set; }
}
