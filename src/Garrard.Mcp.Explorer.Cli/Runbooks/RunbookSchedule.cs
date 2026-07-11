namespace Garrard.Mcp.Explorer.Cli.Runbooks;

/// <summary>Shared schedule block for MCP and HTTP runbooks.</summary>
public sealed class RunbookSchedule
{
    /// <summary>Explicit run count. When null, a configured everySeconds/forSeconds window determines the count.</summary>
    public int? Repeat { get; set; }
    public int? EverySeconds { get; set; }
    public int? ForSeconds { get; set; }
}

public static class RunbookSchedulePlanner
{
    public static int ComputeRunCount(RunbookSchedule schedule)
    {
        ArgumentNullException.ThrowIfNull(schedule);

        if (schedule.EverySeconds is not > 0 || schedule.ForSeconds is not > 0)
            return schedule.Repeat ?? 1;

        var byWindow = (schedule.ForSeconds.Value / schedule.EverySeconds.Value) + 1;

        // An explicit repeat caps the window; without one the window runs to completion.
        return schedule.Repeat is int repeat ? Math.Min(repeat, byWindow) : byWindow;
    }

    public static TimeSpan? ComputeDelay(RunbookSchedule schedule)
        => schedule.EverySeconds is > 0 ? TimeSpan.FromSeconds(schedule.EverySeconds.Value) : null;
}
