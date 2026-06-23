namespace Garrard.Mcp.Explorer.Cli.Runbooks;

public static class McpRunbookSchedulePlanner
{
    public static int ComputeRunCount(McpRunbookSchedule schedule)
    {
        ArgumentNullException.ThrowIfNull(schedule);

        var repeat = schedule.Repeat ?? 1;

        if (schedule.EverySeconds is not > 0 || schedule.ForSeconds is not > 0)
            return repeat;

        var byWindow = (schedule.ForSeconds.Value / schedule.EverySeconds.Value) + 1;
        return Math.Min(repeat, byWindow);
    }

    public static TimeSpan? ComputeDelay(McpRunbookSchedule schedule)
        => schedule.EverySeconds is > 0 ? TimeSpan.FromSeconds(schedule.EverySeconds.Value) : null;
}
