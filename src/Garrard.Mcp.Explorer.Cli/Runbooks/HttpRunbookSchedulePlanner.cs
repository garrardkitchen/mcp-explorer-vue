namespace Garrard.Mcp.Explorer.Cli.Runbooks;

public static class HttpRunbookSchedulePlanner
{
    public static int ComputeRunCount(HttpRunbookSchedule schedule)
    {
        ArgumentNullException.ThrowIfNull(schedule);

        var repeat = schedule.Repeat ?? 1;

        if (schedule.EverySeconds is not > 0 || schedule.ForSeconds is not > 0)
            return repeat;

        var byWindow = (schedule.ForSeconds.Value / schedule.EverySeconds.Value) + 1;
        return Math.Min(repeat, byWindow);
    }

    public static TimeSpan? ComputeDelay(HttpRunbookSchedule schedule)
        => schedule.EverySeconds is > 0 ? TimeSpan.FromSeconds(schedule.EverySeconds.Value) : null;
}
