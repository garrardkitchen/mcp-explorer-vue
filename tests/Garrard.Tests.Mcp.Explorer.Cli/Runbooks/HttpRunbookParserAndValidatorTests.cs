using Garrard.Mcp.Explorer.Cli.Runbooks;

namespace Garrard.Tests.Mcp.Explorer.Cli.Runbooks;

public class HttpRunbookParserAndValidatorTests
{
    [Fact]
    public void Parse_ValidYaml_ReturnsRunbook()
    {
        const string yaml = """
version: "1"
steps:
  - id: first
    endpoint: weather
    inputs:
      city: London
schedule:
  repeat: 2
""";

        var parser = new HttpRunbookParser();
        var runbook = parser.Parse(yaml);

        Assert.Single(runbook.Steps);
        Assert.Equal("weather", runbook.Steps[0].Endpoint);
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
}
