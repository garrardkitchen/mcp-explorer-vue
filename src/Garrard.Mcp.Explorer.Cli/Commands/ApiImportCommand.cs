using Garrard.Mcp.Explorer.Core.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace Garrard.Mcp.Explorer.Cli.Commands;

public sealed class ApiImportCommand : AsyncCommand<ApiImportCommand.Settings>
{
    private readonly IHttpApiStore _store;
    private readonly IHttpApiExportService _exporter;

    public ApiImportCommand(IHttpApiStore store, IHttpApiExportService exporter)
    {
        _store    = store;
        _exporter = exporter;
    }

    public sealed class Settings : CommandSettings
    {
        [CommandOption("-f|--file")] [Description("Path to the export .json file")] public string? File { get; init; }
        [CommandOption("-p|--password")] [Description("Decryption password")] public string? Password { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(settings.File) || !System.IO.File.Exists(settings.File))
        {
            AnsiConsole.MarkupLine("[red]Specify a valid export file path with --file[/]");
            return 1;
        }

        var password = settings.Password;
        if (string.IsNullOrEmpty(password))
        {
            password = AnsiConsole.Prompt(new TextPrompt<string>("Import password:").Secret());
        }

        Core.Interfaces.HttpApiExportPayload payload;
        try
        {
            var json = await System.IO.File.ReadAllTextAsync(settings.File);
            payload  = System.Text.Json.JsonSerializer.Deserialize<Core.Interfaces.HttpApiExportPayload>(json,
                new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase })!;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Failed to read export file: {Markup.Escape(ex.Message)}[/]");
            return 1;
        }

        IReadOnlyList<Core.Domain.HttpApi.HttpApiDefinition> definitions;
        try { definitions = _exporter.Decrypt(payload, password); }
        catch (InvalidOperationException ex)
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
            return 1;
        }

        var all = await _store.GetAllDefinitionsAsync();
        var existing = all.Select(d => d.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var imported = 0;
        foreach (var def in definitions)
        {
            var finalName = def.Name;
            if (existing.Contains(finalName))
            {
                var v = 2;
                while (existing.Contains($"{def.Name} (v{v})")) v++;
                finalName = $"{def.Name} (v{v})";
            }
            def.Id        = Guid.NewGuid().ToString();
            def.Name      = finalName;
            def.CreatedAt = DateTime.UtcNow;
            def.LastUpdatedAt = null;

            await _store.SaveDefinitionAsync(def);
            existing.Add(finalName);
            imported++;
        }

        AnsiConsole.MarkupLine($"[green]✓ Imported {imported} of {definitions.Count} definition(s)[/]");
        return 0;
    }
}
