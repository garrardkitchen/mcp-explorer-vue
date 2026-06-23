using Garrard.Mcp.Explorer.Cli.Runbooks;

namespace Garrard.Tests.Mcp.Explorer.Cli.Runbooks;

public class McpRunbookParserAndValidatorTests
{
    [Fact]
    public void Parse_ValidYaml_ReturnsRunbook()
    {
        const string yaml = """
version: "1"
defaultConnection: local
connections:
  - name: local
    endpoint: https://example.com/mcp
    auth:
      type: bearer
      token: abc123
steps:
  - id: first
    tool: echo
    params:
      message: hello
schedule:
  repeat: 2
""";

        var parser = new McpRunbookParser();
        var runbook = parser.Parse(yaml);

        Assert.Equal("local", runbook.DefaultConnection);
        Assert.Single(runbook.Steps);
        Assert.Equal("echo", runbook.Steps[0].Tool);
    }

    [Fact]
    public void Validate_InvalidRunbook_ReturnsErrors()
    {
        var runbook = new McpRunbook
        {
            DefaultConnection = "missing",
            Steps = [new McpRunbookStep { Id = "bad id", Tool = "" }]
        };

        var errors = McpRunbookValidator.Validate(runbook);

        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("Default connection", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, e => e.Contains("invalid", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_InvalidAssertionOperator_ReturnsErrors()
    {
        var runbook = new McpRunbook
        {
            DefaultConnection = "local",
            Connections =
            [
                new McpRunbookConnection
                {
                    Name = "local",
                    Endpoint = "https://example",
                    Auth = new McpRunbookAuth { Type = "bearer", Token = "token" }
                }
            ],
            Steps =
            [
                new McpRunbookStep
                {
                    Id = "step1",
                    Tool = "echo",
                    Assert = new RunbookAssertion { Operator = "matches", Value = "ok" }
                }
            ]
        };

        var errors = McpRunbookValidator.Validate(runbook);

        Assert.Contains(errors, e => e.Contains("assert.operator", StringComparison.OrdinalIgnoreCase));
    }
}
