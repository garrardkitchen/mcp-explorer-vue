using Garrard.Mcp.Explorer.Cli.Runbooks;
using System.Text.Json;

namespace Garrard.Tests.Mcp.Explorer.Cli.Runbooks;

public class McpRunbookSchedulingAndTemplateTests
{
    [Fact]
    public void ComputeRunCount_UsesWindowAndRepeatCap()
    {
        var schedule = new McpRunbookSchedule
        {
            Repeat = 10,
            EverySeconds = 5,
            ForSeconds = 12
        };

        var runCount = McpRunbookSchedulePlanner.ComputeRunCount(schedule);

        Assert.Equal(3, runCount);
    }

    [Fact]
    public void ResolveObject_ReplacesStepResultTokens()
    {
        var resolver = new McpRunbookTemplateResolver();
        var previousResult = JsonSerializer.Deserialize<JsonElement>("{" + "\"user\":{\"name\":\"Garrard\"}}" );

        var parameters = new Dictionary<string, object?>
        {
            ["name"] = "{{ steps.first.user.name }}"
        };

        var resolved = (Dictionary<string, object?>)resolver.ResolveObject(
            parameters,
            new Dictionary<string, object?> { ["first"] = previousResult },
            iterationIndex: 0)!;

        Assert.Equal("Garrard", resolved["name"]);
    }

    [Fact]
    public void ResolveObject_ReplacesIterationToken()
    {
        var resolver = new McpRunbookTemplateResolver();
        var value = resolver.ResolveObject("run-{{iteration.index}}", new Dictionary<string, object?>(), 4);

        Assert.Equal("run-4", value);
    }
}
