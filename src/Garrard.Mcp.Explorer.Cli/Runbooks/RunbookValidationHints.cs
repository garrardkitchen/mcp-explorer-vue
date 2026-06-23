using YamlDotNet.Core;

namespace Garrard.Mcp.Explorer.Cli.Runbooks;

public static class RunbookValidationHints
{
    public static IReadOnlyList<string> BuildParseHints(Exception ex)
    {
        var hints = new List<string>();

        if (ex is YamlException)
        {
            hints.Add("Check YAML indentation and list markers ('-').");
            hints.Add("Ensure keys use camelCase and values containing ':' are quoted.");
            hints.Add("Validate with --validate-only before running the runbook.");
        }

        return hints;
    }

    public static IReadOnlyList<string> BuildValidationHints(IEnumerable<string> errors)
    {
        var hints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var error in errors)
        {
            if (error.Contains("step", StringComparison.OrdinalIgnoreCase) && error.Contains("id", StringComparison.OrdinalIgnoreCase))
                hints.Add("Give every step a unique id using letters, numbers, '-' or '_'.");

            if (error.Contains("default connection", StringComparison.OrdinalIgnoreCase)
                || error.Contains("unknown connection", StringComparison.OrdinalIgnoreCase))
                hints.Add("Set defaultConnection to a declared connection name or set step.connection explicitly.");

            if (error.Contains("endpoint", StringComparison.OrdinalIgnoreCase))
                hints.Add("For HTTP runbooks, set endpoint or endpointId on every step.");

            if (error.Contains("schedule", StringComparison.OrdinalIgnoreCase))
                hints.Add("Use schedule.repeat >= 1, and pair schedule.forSeconds with schedule.everySeconds.");

            if (error.Contains("assert", StringComparison.OrdinalIgnoreCase)
                || error.Contains("operator", StringComparison.OrdinalIgnoreCase))
                hints.Add("Supported assert operators are equals, notEquals, contains, and exists.");
        }

        return hints.ToList();
    }
}
