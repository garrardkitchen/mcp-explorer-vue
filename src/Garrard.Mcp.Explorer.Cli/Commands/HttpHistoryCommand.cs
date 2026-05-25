using Garrard.Mcp.Explorer.Core.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace Garrard.Mcp.Explorer.Cli.Commands;

public sealed class HttpHistoryCommand : AsyncCommand<HttpHistoryCommand.Settings>
{
    private readonly IHttpApiStore _store;
    private readonly IHttpApiSnapshotStore _snapshots;

    public HttpHistoryCommand(IHttpApiStore store, IHttpApiSnapshotStore snapshots)
    {
        _store = store;
        _snapshots = snapshots;
    }

    public sealed class Settings : CommandSettings
    {
        [CommandOption("--endpoint")] [Description("Endpoint name filter (partial match). Omit for global history.")] public string? EndpointName { get; init; }
        [CommandOption("--limit")] [Description("Maximum records to display (default: 20)")] [DefaultValue(20)] public int Limit { get; init; } = 20;
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken ct)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("When")
            .AddColumn("Endpoint")
            .AddColumn("Status")
            .AddColumn("Latency")
            .AddColumn("Schema")
            .AddColumn("Error");

        if (!string.IsNullOrEmpty(settings.EndpointName))
        {
            var all = await _store.GetAllDefinitionsAsync();
            var def = all.FirstOrDefault(d => d.Name.Contains(settings.EndpointName, StringComparison.OrdinalIgnoreCase));
            if (def is null)
            {
                AnsiConsole.MarkupLine($"[red]No endpoint matching '{settings.EndpointName}'[/]");
                return 1;
            }
            var history = await _snapshots.GetHistoryAsync(def.Id, settings.Limit);
            foreach (var r in history)
            {
                var sc = r.StatusCode >= 200 && r.StatusCode < 300 ? "green" : r.StatusCode >= 500 ? "red" : "yellow";
                var schema = r.SchemaMatchedSnapshot is null ? "[dim]—[/]"
                    : r.SchemaMatchedSnapshot == true ? "[green]Match[/]" : "[red]Drift[/]";
                table.AddRow(
                    r.InvokedAt.ToLocalTime().ToString("g"),
                    Markup.Escape(r.EndpointName),
                    $"[{sc}]{r.StatusCode}[/]",
                    $"{r.LatencyMs} ms",
                    schema,
                    Markup.Escape(r.ErrorMessage ?? string.Empty)
                );
            }
        }
        else
        {
            var history = await _snapshots.GetGlobalHistoryAsync(settings.Limit);
            foreach (var r in history)
            {
                var sc = r.StatusCode >= 200 && r.StatusCode < 300 ? "green" : r.StatusCode >= 500 ? "red" : "yellow";
                var schema = r.SchemaMatchedSnapshot is null ? "[dim]—[/]"
                    : r.SchemaMatchedSnapshot == true ? "[green]Match[/]" : "[red]Drift[/]";
                table.AddRow(
                    r.InvokedAt.ToLocalTime().ToString("g"),
                    Markup.Escape(r.EndpointName),
                    $"[{sc}]{r.StatusCode}[/]",
                    $"{r.LatencyMs} ms",
                    schema,
                    Markup.Escape(r.ErrorMessage ?? string.Empty)
                );
            }
        }

        AnsiConsole.Write(table);
        return 0;
    }
}
