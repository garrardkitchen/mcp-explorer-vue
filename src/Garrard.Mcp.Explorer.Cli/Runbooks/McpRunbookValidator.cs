using System.Text.RegularExpressions;

namespace Garrard.Mcp.Explorer.Cli.Runbooks;

public static class McpRunbookValidator
{
    private static readonly Regex StepIdPattern = new("^[a-zA-Z0-9_-]+$", RegexOptions.Compiled);

    public static IReadOnlyList<string> Validate(McpRunbook runbook)
    {
        ArgumentNullException.ThrowIfNull(runbook);

        var errors = new List<string>();

        if (runbook.Steps.Count == 0)
            errors.Add("Runbook must include at least one step.");

        var connectionNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var connection in runbook.Connections)
        {
            if (string.IsNullOrWhiteSpace(connection.Name))
                errors.Add("Connection name is required.");
            else if (!connectionNames.Add(connection.Name))
                errors.Add($"Connection '{connection.Name}' is duplicated.");

            if (string.IsNullOrWhiteSpace(connection.Endpoint))
                errors.Add($"Connection '{connection.Name}' endpoint is required.");

            if (connection.Auth is null)
                errors.Add($"Connection '{connection.Name}' auth block is required.");
        }

        if (!string.IsNullOrWhiteSpace(runbook.DefaultConnection)
            && !connectionNames.Contains(runbook.DefaultConnection))
        {
            errors.Add($"Default connection '{runbook.DefaultConnection}' is not declared in the runbook.");
        }

        var stepIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var step in runbook.Steps)
        {
            if (string.IsNullOrWhiteSpace(step.Id))
                errors.Add("Each step requires an id.");
            else
            {
                if (!StepIdPattern.IsMatch(step.Id))
                    errors.Add($"Step id '{step.Id}' is invalid. Use letters, numbers, '-' or '_'.");
                if (!stepIds.Add(step.Id))
                    errors.Add($"Step id '{step.Id}' is duplicated.");
            }

            if (string.IsNullOrWhiteSpace(step.Tool))
                errors.Add($"Step '{step.Id}' tool is required.");

            if (!string.IsNullOrWhiteSpace(step.Connection) && !connectionNames.Contains(step.Connection))
                errors.Add($"Step '{step.Id}' references unknown connection '{step.Connection}'.");

            if (step.MaxRetries < 0)
                errors.Add($"Step '{step.Id}' maxRetries cannot be negative.");
        }

        var repeat = runbook.Schedule.Repeat ?? 1;
        if (repeat < 1 || repeat > 10_000)
            errors.Add("schedule.repeat must be between 1 and 10000.");

        if (runbook.Schedule.EverySeconds is < 0)
            errors.Add("schedule.everySeconds cannot be negative.");

        if (runbook.Schedule.ForSeconds is < 0)
            errors.Add("schedule.forSeconds cannot be negative.");

        if (runbook.Schedule.ForSeconds.HasValue && !runbook.Schedule.EverySeconds.HasValue)
            errors.Add("schedule.forSeconds requires schedule.everySeconds.");

        return errors;
    }
}
