using Garrard.Mcp.Explorer.Core.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace Garrard.Mcp.Explorer.Cli.Commands;

public sealed class HttpInvokeCommand : AsyncCommand<HttpInvokeCommand.Settings>
{
    private readonly IHttpApiStore _store;
    private readonly IHttpApiInvoker _invoker;
    private readonly ISchemaInferenceService _schema;
    private readonly IHttpApiSnapshotStore _snapshots;

    public HttpInvokeCommand(
        IHttpApiStore store,
        IHttpApiInvoker invoker,
        ISchemaInferenceService schema,
        IHttpApiSnapshotStore snapshots)
    {
        _store = store;
        _invoker = invoker;
        _schema = schema;
        _snapshots = snapshots;
    }

    public sealed class Settings : CommandSettings
    {
        [CommandOption("--id")] [Description("Endpoint ID")] public string? Id { get; init; }
        [CommandOption("--name")] [Description("Endpoint name (partial match)")] public string? Name { get; init; }
        [CommandOption("--bookmark")] [Description("Save a snapshot bookmark after invocation")] public bool Bookmark { get; init; }
        [CommandOption("--label")] [Description("Label for the bookmark")] public string? Label { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken ct)
    {
        var def = await ResolveEndpointAsync(settings.Id, settings.Name);
        if (def is null) return 1;

        HttpApiInvokeResult result = null!;
        await AnsiConsole.Status().StartAsync($"Invoking [bold]{def.Name}[/]…", async _ =>
        {
            result = await _invoker.InvokeAsync(def);
        });

        var schema = _schema.InferSchema(result.Body);

        // ── Status panel ───────────────────────────────────────────────────────
        var statusColor = result.StatusCode is >= 200 and < 300 ? "green" : result.StatusCode >= 500 ? "red" : "yellow";
        AnsiConsole.MarkupLine($"[bold]Status:[/] [{statusColor}]{result.StatusCode}[/]   [dim]{result.LatencyMs} ms[/]");

        if (!string.IsNullOrEmpty(result.ErrorMessage))
            AnsiConsole.MarkupLine($"[red]Error: {Markup.Escape(result.ErrorMessage)}[/]");

        // ── Body ───────────────────────────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(result.Body))
        {
            AnsiConsole.Write(new Panel(new Text(result.Body)).Header("Body").Border(BoxBorder.Rounded));
        }

        // ── Inferred schema ────────────────────────────────────────────────────
        var schemaJson = System.Text.Json.JsonSerializer.Serialize(schema, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        AnsiConsole.Write(new Panel(new Text(schemaJson)).Header("Inferred Schema").Border(BoxBorder.Rounded));

        // ── Bookmark ───────────────────────────────────────────────────────────
        if (settings.Bookmark)
        {
            var snapshot = new Core.Domain.HttpApi.HttpResponseSnapshot
            {
                EndpointId       = def.Id,
                EndpointName     = def.Name,
                StatusCode       = result.StatusCode,
                LatencyMs        = result.LatencyMs,
                ResponseHeaders  = result.ResponseHeaders,
                InferredSchema   = schema,
                RawBodyTruncated = result.Body,
                ContentType      = result.ContentType,
                Label            = settings.Label
            };
            await _snapshots.AppendSnapshotAsync(snapshot);
            AnsiConsole.MarkupLine($"[green]✓ Snapshot saved ({snapshot.Id[..8]})[/]");
        }

        // ── Invocation record ──────────────────────────────────────────────────
        await _snapshots.AppendInvocationAsync(new Core.Domain.HttpApi.HttpApiInvocationRecord
        {
            EndpointId   = def.Id,
            EndpointName = def.Name,
            StatusCode   = result.StatusCode,
            LatencyMs    = result.LatencyMs,
            SchemaHash   = _schema.ComputeSchemaHash(schema),
            ErrorMessage = result.ErrorMessage
        });

        return result.IsSuccess ? 0 : 1;
    }

    private async Task<Core.Domain.HttpApi.HttpApiDefinition?> ResolveEndpointAsync(string? id, string? name)
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
