using Garrard.Mcp.Explorer.Core.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace Garrard.Mcp.Explorer.Cli.Commands;

public sealed class McpPromptsCommand : AsyncCommand<McpPromptsCommand.Settings>
{
    private readonly IUserPreferencesStore _store;
    private readonly IConnectionService _connections;

    public McpPromptsCommand(IUserPreferencesStore store, IConnectionService connections)
    {
        _store = store;
        _connections = connections;
    }

    public sealed class Settings : CommandSettings
    {
        [CommandOption("--name")]
        [Description("MCP connection name (exact or partial match)")]
        public string? Name { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(settings.Name))
        {
            AnsiConsole.MarkupLine("[red]Specify --name <connection_name>[/]");
            return 1;
        }

        var prefs = await _store.LoadAsync(ct);
        var def = McpCommandHelper.ResolveConnection(prefs, settings.Name);
        if (def is null) return 1;

        McpCommandHelper.WarnIfOAuth(def);

        IActiveConnection conn = null!;
        await AnsiConsole.Status().StartAsync($"Connecting to [bold]{Markup.Escape(def.Name)}[/]…", async _ =>
        {
            conn = await _connections.ConnectAsync(def, ct);
        });

        try
        {
            var prompts = conn.Prompts;
            if (prompts.Count == 0)
            {
                AnsiConsole.MarkupLine("[dim]No prompts found.[/]");
                return 0;
            }

            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("Name")
                .AddColumn("Description")
                .AddColumn("Arguments");

            foreach (var p in prompts.OrderBy(p => p.Name))
            {
                var args = p.Arguments.Count == 0
                    ? "[dim]none[/]"
                    : string.Join(", ", p.Arguments.Select(a => a.Required ? $"[bold]{Markup.Escape(a.Name)}[/]" : Markup.Escape(a.Name)));

                table.AddRow(
                    Markup.Escape(p.Name),
                    Markup.Escape(string.IsNullOrWhiteSpace(p.Description) ? "—" : p.Description),
                    args
                );
            }

            AnsiConsole.Write(table);
            return 0;
        }
        finally
        {
            try { await _connections.DisconnectAsync(def.Name, CancellationToken.None); } catch { /* best-effort cleanup */ }
        }
    }
}
