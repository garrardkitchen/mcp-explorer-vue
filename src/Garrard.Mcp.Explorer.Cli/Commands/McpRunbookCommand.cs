using Garrard.Mcp.Explorer.Cli.Runbooks;
using Garrard.Mcp.Explorer.Core.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Text.Json;

namespace Garrard.Mcp.Explorer.Cli.Commands;

public sealed class McpRunbookCommand : AsyncCommand<McpRunbookCommand.Settings>
{
    private readonly McpRunbookParser _parser = new();
    private readonly McpRunbookExecutor _executor;

    public McpRunbookCommand(
        IConnectionService connectionService,
        IUserPreferencesStore preferencesStore,
        IKeyVaultSecretResolver secretResolver)
    {
        _executor = new McpRunbookExecutor(
            connectionService,
            preferencesStore,
            new McpRunbookConnectionFactory(secretResolver),
            new McpRunbookTemplateResolver());
    }

    public sealed class Settings : CommandSettings
    {
        [CommandOption("--file")]
        [Description("Path to YAML runbook file")]
        public string? File { get; init; }

        [CommandOption("--validate-only")]
        [Description("Validate the runbook and exit")]
        public bool ValidateOnly { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.File))
        {
            AnsiConsole.MarkupLine("[red]Specify --file <runbook.yaml>[/]");
            return 1;
        }

        var fullPath = Path.GetFullPath(settings.File);
        if (!File.Exists(fullPath))
        {
            AnsiConsole.MarkupLine($"[red]Runbook file not found:[/] {Markup.Escape(fullPath)}");
            return 1;
        }

        McpRunbook runbook;
        try
        {
            var yaml = await File.ReadAllTextAsync(fullPath, cancellationToken).ConfigureAwait(false);
            runbook = _parser.Parse(yaml);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Failed to parse runbook:[/] {Markup.Escape(ex.Message)}");
            return 1;
        }

        var errors = McpRunbookValidator.Validate(runbook);
        if (errors.Count > 0)
        {
            AnsiConsole.MarkupLine("[red]Runbook validation failed:[/]");
            foreach (var error in errors)
                AnsiConsole.MarkupLine($"  • {Markup.Escape(error)}");
            return 1;
        }

        if (settings.ValidateOnly)
        {
            AnsiConsole.MarkupLine("[green]Runbook is valid.[/]");
            return 0;
        }

        var results = await _executor.ExecuteAsync(runbook, message =>
        {
            if (!Console.IsOutputRedirected)
                AnsiConsole.MarkupLine($"[grey]{Markup.Escape(message)}[/]");
        }, cancellationToken).ConfigureAwait(false);

        var json = JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });
        AnsiConsole.Write(new Panel(new Text(json)).Header("Runbook results").Border(BoxBorder.Rounded));

        return 0;
    }
}
