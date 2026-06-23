namespace Garrard.Mcp.Explorer.Cli.Runbooks;

public sealed class HttpRunbook
{
    public string Version { get; set; } = "1";
    public bool ContinueOnAssertFailure { get; set; }
    public List<HttpRunbookStep> Steps { get; set; } = [];
    public HttpRunbookSchedule Schedule { get; set; } = new();
}

public sealed class HttpRunbookStep
{
    public string Id { get; set; } = string.Empty;
    public string? EndpointId { get; set; }
    public string? Endpoint { get; set; }
    public Dictionary<string, object?> Inputs { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public RunbookAssertion? Assert { get; set; }
    public bool? ContinueOnAssertFailure { get; set; }
    public int MaxRetries { get; set; }
    public bool ContinueOnError { get; set; }
    public bool UseLocalhost { get; set; }
}

public sealed class HttpRunbookSchedule
{
    public int? Repeat { get; set; } = 1;
    public int? EverySeconds { get; set; }
    public int? ForSeconds { get; set; }
}
