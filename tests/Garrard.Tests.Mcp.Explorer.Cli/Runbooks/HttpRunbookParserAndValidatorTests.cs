using Garrard.Mcp.Explorer.Cli.Runbooks;

namespace Garrard.Tests.Mcp.Explorer.Cli.Runbooks;

public class HttpRunbookParserAndValidatorTests
{
    [Fact]
    public void Parse_ValidYaml_ReturnsRunbook()
    {
        const string yaml = """
version: "1"
defaultConnection: httpbin
connections:
  - name: httpbin
    endpoint: https://httpbin.org/get
    auth:
      type: custom
      headers: []
steps:
  - id: first
    inputs:
      city: London
schedule:
  repeat: 2
""";

        var parser = new HttpRunbookParser();
        var runbook = parser.Parse(yaml);

        Assert.Single(runbook.Steps);
        Assert.Equal("httpbin", runbook.DefaultConnection);
        Assert.Single(runbook.Connections);
        Assert.Null(runbook.Steps[0].Endpoint);
    }

    [Fact]
    public void Validate_InvalidRunbook_ReturnsErrors()
    {
        var runbook = new HttpRunbook
        {
            Steps = [new HttpRunbookStep { Id = "bad id", Endpoint = "", EndpointId = "", MaxRetries = -1 }]
        };

        var errors = HttpRunbookValidator.Validate(runbook);

        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("invalid", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, e => e.Contains("endpoint", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_InvalidAssertionOperator_ReturnsErrors()
    {
        var runbook = new HttpRunbook
        {
            Steps =
            [
                new HttpRunbookStep
                {
                    Id = "step1",
                    Endpoint = "Weather API",
                    Assert = new RunbookAssertion { Operator = "matches", Value = "ok" }
                }
            ]
        };

        var errors = HttpRunbookValidator.Validate(runbook);

        Assert.Contains(errors, e => e.Contains("assert.operator", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_WithDefaultConnection_AllowsStepWithoutEndpoint()
    {
        var runbook = new HttpRunbook
        {
            DefaultConnection = "httpbin",
            Connections =
            [
                new HttpRunbookConnection
                {
                    Name = "httpbin",
                    Endpoint = "https://httpbin.org/get",
                    Auth = new HttpRunbookAuth
                    {
                        Type = "custom",
                        Headers = []
                    }
                }
            ],
            Steps = [new HttpRunbookStep { Id = "step1" }]
        };

        var errors = HttpRunbookValidator.Validate(runbook);

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_UnsupportedConnectionAuthType_ReturnsError()
    {
        var runbook = new HttpRunbook
        {
            Connections =
            [
                new HttpRunbookConnection
                {
                    Name = "httpbin",
                    Endpoint = "https://httpbin.org/get",
                    Auth = new HttpRunbookAuth
                    {
                        Type = "oauth2"
                    }
                }
            ],
            Steps = [new HttpRunbookStep { Id = "step1", Endpoint = "httpbin" }]
        };

        var errors = HttpRunbookValidator.Validate(runbook);

        Assert.Contains(errors, e => e.Contains("auth.type", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Parse_NullCollectionNodes_NormalizesListsForValidation()
    {
        const string yaml = """
version: "1"
connections:
steps:
""";

        var parser = new HttpRunbookParser();
        var runbook = parser.Parse(yaml);
        var errors = HttpRunbookValidator.Validate(runbook);

        Assert.NotNull(runbook.Connections);
        Assert.NotNull(runbook.Steps);
        Assert.Contains(errors, e => e.Contains("at least one step", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Parse_NullListItems_NormalizesListsForValidation()
    {
        const string yaml = """
version: "1"
connections:
  -
steps:
  -
""";

        var parser = new HttpRunbookParser();
        var runbook = parser.Parse(yaml);
        var errors = HttpRunbookValidator.Validate(runbook);

        Assert.Empty(runbook.Connections);
        Assert.Empty(runbook.Steps);
        Assert.Contains(errors, e => e.Contains("at least one step", StringComparison.OrdinalIgnoreCase));
    }
}
