using Garrard.Mcp.Explorer.Core.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace Garrard.Mcp.Explorer.Cli.Commands;

public sealed class ApiExportCommand : AsyncCommand<ApiExportCommand.Settings>
{
    private readonly IHttpApiStore _store;
    private readonly IHttpApiExportService _exporter;

    public ApiExportCommand(IHttpApiStore store, IHttpApiExportService exporter)
    {
        _store    = store;
        _exporter = exporter;
    }

    public sealed class Settings : CommandSettings
    {
        [CommandOption("-o|--output")] [Description("Output file path (default: http-apis-export.json)")] public string Output { get; init; } = "http-apis-export.json";
        [CommandOption("-p|--password")] [Description("Encryption password")] public string? Password { get; init; }
        [CommandOption("--names")] [Description("Comma-separated list of definition names to export (default: all)")] public string? Names { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken ct)
    {
        var password = settings.Password;
        if (string.IsNullOrEmpty(password))
        {
            password = AnsiConsole.Prompt(new TextPrompt<string>("Export password:").Secret());
        }

        var all = await _store.GetAllDefinitionsAsync();
        var toExport = string.IsNullOrWhiteSpace(settings.Names)
            ? all.ToList()
            : all.Where(d => settings.Names.Split(',').Any(n => d.Name.Contains(n.Trim(), StringComparison.OrdinalIgnoreCase))).ToList();

        if (toExport.Count == 0) { AnsiConsole.MarkupLine("[yellow]No definitions matched.[/]"); return 1; }

        var payload = _exporter.Encrypt(toExport, password);
        var json    = System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });

        await File.WriteAllTextAsync(settings.Output, json);
        AnsiConsole.MarkupLine($"[green]✓ Exported {toExport.Count} definition(s) to {settings.Output}[/]");
        return 0;
    }
}
