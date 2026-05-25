using Garrard.Mcp.Explorer.Core.Domain.HttpApi;
using Garrard.Mcp.Explorer.Core.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace Garrard.Mcp.Explorer.Cli.Commands;

public sealed class HttpRunCollectionCommand : AsyncCommand<HttpRunCollectionCommand.Settings>
{
    private readonly IHttpApiStore _store;
    private readonly IHttpApiInvoker _invoker;
    private readonly ISchemaInferenceService _schemaInference;
    private readonly ISchemaComparisonService _schemaComparison;
    private readonly IHttpApiSnapshotStore _snapshots;

    public HttpRunCollectionCommand(
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
        [CommandOption("--id")] [Description("Collection ID")] public string? Id { get; init; }
        [CommandOption("--name")] [Description("Collection name (partial match)")] public string? Name { get; init; }
        [CommandOption("--fail-on-breaking")] [Description("Exit with code 1 if any endpoint has breaking changes")] public bool FailOnBreaking { get; init; }
        [CommandOption("--use-localhost")] [Description("Replace host.docker.internal with localhost")] public bool UseLocalhost { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken ct)
    {
        var collection = await ResolveCollectionAsync(settings.Id, settings.Name);
        if (collection is null) return 1;

        var runId      = Guid.NewGuid().ToString();
        var startedAt  = DateTime.UtcNow;
        var sw         = System.Diagnostics.Stopwatch.StartNew();

        if (collection.EndpointIds.Count == 0)
        {
            AnsiConsole.MarkupLine($"[yellow]Collection '{collection.Name}' has no endpoints — nothing to run.[/]");
            return 0;
        }

        AnsiConsole.MarkupLine($"[bold]Running collection:[/] {collection.Name} ({collection.EndpointIds.Count} endpoints)");

        var resultTable = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Endpoint")
            .AddColumn("Status")
            .AddColumn("Latency")
            .AddColumn("Result")
            .AddColumn("Schema Δ");

        var hasBreaking  = false;
        var successCount = 0;
        var totalCount   = 0;
        var degradeCount = 0;
        var summaries    = new List<Core.Domain.HttpApi.HttpApiCollectionEndpointRunSummary>();

        await AnsiConsole.Progress()
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("Running endpoints…", maxValue: collection.EndpointIds.Count);

                foreach (var endpointId in collection.EndpointIds)
                {
                    var def = await _store.GetDefinitionAsync(endpointId);
                    if (def is null)
                    {
                        resultTable.AddRow(endpointId, "—", "—", "[dim]Skipped (not found)[/]", "—");
                        summaries.Add(new Core.Domain.HttpApi.HttpApiCollectionEndpointRunSummary
                        {
                            EndpointId   = endpointId,
                            EndpointName = endpointId,
                            Skipped      = true
                        });
                        task.Increment(1);
                        continue;
                    }

                    if (settings.UseLocalhost)
                    {
                        def = ApplyLocalhostTransform(def);
                    }

                    var result = await _invoker.InvokeAsync(def);
                    var schema = _schemaInference.InferSchema(result.Body);
                    var hash   = _schemaInference.ComputeSchemaHash(schema);

                    HttpSchemaComparisonResult? comparison = null;
                    var targetId = def.GoldenSnapshotId;
                    HttpResponseSnapshot? baseline = null;

                    if (!string.IsNullOrEmpty(targetId))
                        baseline = await _snapshots.GetSnapshotAsync(endpointId, targetId);
                    if (baseline is null)
                    {
                        var allSnaps = await _snapshots.GetSnapshotsAsync(endpointId);
                        baseline = allSnaps.MaxBy(s => s.CapturedAt);
                    }
                    if (baseline is not null)
                        comparison = _schemaComparison.Compare(def, baseline, result.StatusCode, result.LatencyMs, schema);

                    var isCountedSuccess = result.IsSuccess && comparison?.IsBreaking != true;
                    totalCount++;
                    if (isCountedSuccess) successCount++;

                    summaries.Add(new Core.Domain.HttpApi.HttpApiCollectionEndpointRunSummary
                    {
                        EndpointId                  = def.Id,
                        EndpointName                = def.Name,
                        StatusCode                  = result.StatusCode,
                        LatencyMs                   = result.LatencyMs,
                        IsSuccess                   = isCountedSuccess,
                        Skipped                     = false,
                        ErrorMessage                = result.ErrorMessage,
                        ComparisonHasBaseline       = comparison is not null,
                        ComparisonIsBreaking        = comparison?.IsBreaking,
                        ComparisonIsDegraded        = comparison?.IsDegraded,
                        ComparisonRemovedProperties = comparison?.RemovedProperties,
                        ComparisonAddedProperties   = comparison?.AddedProperties,
                        ComparisonChangedTypes      = comparison?.ChangedTypes
                    });

                    var invRecord = new HttpApiInvocationRecord
                    {
                        EndpointId            = def.Id,
                        EndpointName          = def.Name,
                        StatusCode            = result.StatusCode,
                        LatencyMs             = result.LatencyMs,
                        SchemaHash            = hash,
                        SchemaMatchedSnapshot = comparison is null ? null : !comparison.IsBreaking,
                        CollectionRunId       = runId,
                        ErrorMessage          = result.ErrorMessage,
                        InvokedVia            = HttpApiInvocationSource.Cli
                    };
                    CliInvocationHelper.ApplyRequestResponse(invRecord, def, result);
                    await _snapshots.AppendInvocationAsync(invRecord);

                    var statusColor = result.IsSuccess ? "green" : "red";
                    var resultLabel = comparison is null ? "[dim]No baseline[/]"
                        : comparison.IsBreaking   ? "[red]🔴 Breaking[/]"
                        : comparison.IsDegraded   ? "[yellow]🟡 Degraded[/]"
                        : "[green]🟢 OK[/]";

                    var schemaDelta = comparison is null ? "—" :
                        $"[red]-{comparison.RemovedProperties.Count}[/] [green]+{comparison.AddedProperties.Count}[/] [yellow]~{comparison.ChangedTypes.Count}[/]";

                    resultTable.AddRow(
                        Markup.Escape(def.Name),
                        $"[{statusColor}]{result.StatusCode}[/]",
                        $"{result.LatencyMs} ms",
                        resultLabel,
                        schemaDelta
                    );

                    if (comparison?.IsBreaking == true) { hasBreaking = true; }
                    else if (comparison?.IsDegraded == true) { degradeCount++; }

                    task.Increment(1);
                }
            });

        sw.Stop();

        // Persist run stats to the collection (without bumping LastUpdatedAt)
        await _store.PatchCollectionRunStatsAsync(
            collection.Id,
            startedAt,
            sw.ElapsedMilliseconds,
            successCount,
            totalCount,
            runId,
            HttpApiInvocationSource.Cli,
            summaries);

        // Best-effort: append to collection run history
        try
        {
            await _snapshots.AppendCollectionRunAsync(new Core.Domain.HttpApi.HttpApiCollectionRunRecord
            {
                RunId             = runId,
                CollectionId      = collection.Id,
                CollectionName    = collection.Name,
                RanAt             = startedAt,
                DurationMs        = sw.ElapsedMilliseconds,
                SuccessCount      = successCount,
                TotalCount        = totalCount,
                InvokedVia        = HttpApiInvocationSource.Cli,
                EndpointSummaries = summaries
            });
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[yellow]Warning: failed to save collection run history: {Markup.Escape(ex.Message)}[/]");
        }

        AnsiConsole.Write(resultTable);
        AnsiConsole.MarkupLine($"[bold]Summary:[/] [green]{successCount} passed[/]  [red]{totalCount - successCount} failed[/]  [yellow]{degradeCount} degraded[/]  [dim]{sw.ElapsedMilliseconds} ms[/]");

        return hasBreaking && settings.FailOnBreaking ? 1 : 0;
    }

    private async Task<Core.Domain.HttpApi.HttpApiCollection?> ResolveCollectionAsync(string? id, string? name)
    {
        if (!string.IsNullOrEmpty(id))
        {
            var c = await _store.GetCollectionAsync(id);
            if (c is null) { AnsiConsole.MarkupLine($"[red]No collection found with ID '{id}'[/]"); return null; }
            return c;
        }
        if (!string.IsNullOrEmpty(name))
        {
            var all = await _store.GetAllCollectionsAsync();
            var c = all.FirstOrDefault(x => x.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
            if (c is null) { AnsiConsole.MarkupLine($"[red]No collection matching '{name}'[/]"); return null; }
            return c;
        }
        AnsiConsole.MarkupLine("[red]Specify --id or --name[/]");
        return null;
    }

    private static Core.Domain.HttpApi.HttpApiDefinition ApplyLocalhostTransform(Core.Domain.HttpApi.HttpApiDefinition def)
    {
        if (def.BaseUrl.Contains("host.docker.internal", StringComparison.OrdinalIgnoreCase))
        {
            def.BaseUrl = def.BaseUrl.Replace("host.docker.internal", "localhost", StringComparison.OrdinalIgnoreCase);
        }
        return def;
    }
}
