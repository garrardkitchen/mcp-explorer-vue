using System.Text.RegularExpressions;

namespace Garrard.Mcp.Explorer.Cli.Runbooks;

public static class HttpRunbookValidator
{
    private static readonly Regex StepIdPattern = new("^[a-zA-Z0-9_-]+$", RegexOptions.Compiled);

    public static IReadOnlyList<string> Validate(HttpRunbook runbook)
    {
        ArgumentNullException.ThrowIfNull(runbook);

        var errors = new List<string>();

        if (runbook.Steps.Count == 0)
            errors.Add("Runbook must include at least one step.");

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

            if (string.IsNullOrWhiteSpace(step.Endpoint) && string.IsNullOrWhiteSpace(step.EndpointId))
                errors.Add($"Step '{step.Id}' must set endpoint or endpointId.");

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
