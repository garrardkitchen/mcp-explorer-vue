using Garrard.Mcp.Explorer.Core.Domain.HttpApi;
using Garrard.Mcp.Explorer.Core.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace Garrard.Mcp.Explorer.Cli.Commands;

public sealed class HttpCompareCommand : AsyncCommand<HttpCompareCommand.Settings>
{
    private readonly IHttpApiStore _store;
    private readonly IHttpApiInvoker _invoker;
    private readonly ISchemaInferenceService _schemaInference;
    private readonly ISchemaComparisonService _schemaComparison;
    private readonly IHttpApiSnapshotStore _snapshots;

    public HttpCompareCommand(
        IHttpApiStore store,
        IHttpApiInvoker invoker,
        ISchemaInferenceService schemaInference,
        ISchemaComparisonService schemaComparison,
        IHttpApiSnapshotStore snapshots)
    {
        _store = store;
        _invoker = invoker;
        _schemaInference = schemaInference;
        _schemaComparison = schemaComparison;
        _snapshots = snapshots;
    }

    public sealed class Settings : CommandSettings
    {
        [CommandOption("--id")] [Description("Endpoint ID")] public string? Id { get; init; }
        [CommandOption("--name")] [Description("Endpoint name (partial match)")] public string? Name { get; init; }
        [CommandOption("--snapshot-id")] [Description("Baseline snapshot ID (default: latest)")] public string? SnapshotId { get; init; }
        [CommandOption("--fail-on-breaking")] [Description("Exit with code 1 when breaking changes detected")] public bool FailOnBreaking { get; init; }
        [CommandOption("--degradation-multiplier")] [Description("Latency ratio threshold for degradation (default: 2.0)")] [DefaultValue(2.0)] public double DegradationMultiplier { get; init; } = 2.0;
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken ct)
    {
        var def = await ResolveEndpointAsync(settings.Id, settings.Name);
        if (def is null) return 1;

        // Resolve baseline
        HttpResponseSnapshot? baseline;
        var targetId = settings.SnapshotId ?? def.GoldenSnapshotId;
        if (!string.IsNullOrEmpty(targetId))
            baseline = await _snapshots.GetSnapshotAsync(def.Id, targetId);
        else
        {
            var all = await _snapshots.GetSnapshotsAsync(def.Id);
            baseline = all.MaxBy(s => s.CapturedAt);
        }

        if (baseline is null)
        {
            AnsiConsole.MarkupLine("[yellow]No baseline snapshot found. Run 'http invoke --bookmark' first.[/]");
            return 1;
        }

        HttpApiInvokeResult result = null!;
        await AnsiConsole.Status().StartAsync($"Comparing [bold]{def.Name}[/] against snapshot {baseline.Id[..8]}…", async _ =>
        {
            result = await _invoker.InvokeAsync(def);
        });

        var schema     = _schemaInference.InferSchema(result.Body);
        var comparison = _schemaComparison.Compare(def, baseline, result.StatusCode, result.LatencyMs, schema, settings.DegradationMultiplier);

        await _snapshots.AppendInvocationAsync(new HttpApiInvocationRecord
        {
            EndpointId            = def.Id,
            EndpointName          = def.Name,
            StatusCode            = result.StatusCode,
            LatencyMs             = result.LatencyMs,
            SchemaHash            = _schemaInference.ComputeSchemaHash(schema),
            SchemaMatchedSnapshot = !comparison.IsBreaking
        });

        // ── Print result ───────────────────────────────────────────────────────
        var statusLine = comparison.IsBreaking
            ? "[bold red]🔴 BREAKING CHANGES DETECTED[/]"
            : comparison.IsDegraded
                ? "[bold yellow]🟡 DEGRADED (latency regression)[/]"
                : "[bold green]🟢 OK — no changes detected[/]";

        AnsiConsole.MarkupLine(statusLine);
        AnsiConsole.MarkupLine($"Status: [bold]{result.StatusCode}[/] (was {baseline.StatusCode})   Latency: [bold]{result.LatencyMs} ms[/] (was {baseline.LatencyMs} ms, ratio {comparison.LatencyRatio:P0})");

        if (comparison.RemovedProperties.Count > 0)
        {
            var t = new Table().Title("[red]Removed Properties[/]").AddColumn("Path");
            foreach (var p in comparison.RemovedProperties) t.AddRow($"[red]{Markup.Escape(p)}[/]");
            AnsiConsole.Write(t);
        }
        if (comparison.AddedProperties.Count > 0)
        {
            var t = new Table().Title("[green]Added Properties[/]").AddColumn("Path");
            foreach (var p in comparison.AddedProperties) t.AddRow($"[green]{Markup.Escape(p)}[/]");
            AnsiConsole.Write(t);
        }
        if (comparison.ChangedTypes.Count > 0)
        {
            var t = new Table().Title("[yellow]Type Changes[/]").AddColumn("Path").AddColumn("Was").AddColumn("Now");
            foreach (var c in comparison.ChangedTypes)
                t.AddRow(Markup.Escape(c.PropertyPath), $"[dim]{c.PreviousType}[/]", $"[yellow]{c.CurrentType}[/]");
            AnsiConsole.Write(t);
        }

        if (comparison.IsBreaking && settings.FailOnBreaking) return 1;
        return 0;
    }

    private async Task<HttpApiDefinition?> ResolveEndpointAsync(string? id, string? name)
    {
        if (!string.IsNullOrEmpty(id))
        {
            var d = await _store.GetDefinitionAsync(id);
            if (d is null) { AnsiConsole.MarkupLine($"[red]No endpoint found with ID '{id}'[/]"); return null; }
            return d;
        }
        if (!string.IsNullOrEmpty(name))
        {
            var all = await _store.GetAllDefinitionsAsync();
            var d = all.FirstOrDefault(x => x.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
            if (d is null) { AnsiConsole.MarkupLine($"[red]No endpoint matching '{name}'[/]"); return null; }
            return d;
        }
        AnsiConsole.MarkupLine("[red]Specify --id or --name[/]");
        return null;
    }
}
