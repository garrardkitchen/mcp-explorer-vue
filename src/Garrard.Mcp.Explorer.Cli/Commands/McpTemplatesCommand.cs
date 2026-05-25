using Garrard.Mcp.Explorer.Core.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace Garrard.Mcp.Explorer.Cli.Commands;

public sealed class McpTemplatesCommand : AsyncCommand<McpTemplatesCommand.Settings>
{
    private readonly IUserPreferencesStore _store;
    private readonly IConnectionService _connections;

    public McpTemplatesCommand(IUserPreferencesStore store, IConnectionService connections)
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
            var templates = conn.ResourceTemplates;
            if (templates.Count == 0)
            {
                AnsiConsole.MarkupLine("[dim]No resource templates found.[/]");
                return 0;
            }

            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("URI Template")
                .AddColumn("Name")
                .AddColumn("Description");

            foreach (var t in templates.OrderBy(t => t.Name))
            {
                table.AddRow(
                    Markup.Escape(t.UriTemplate),
                    Markup.Escape(t.Name),
                    Markup.Escape(string.IsNullOrWhiteSpace(t.Description) ? "—" : t.Description)
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
