using Garrard.Mcp.Explorer.Core.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace Garrard.Mcp.Explorer.Cli.Commands;

public sealed class McpResourcesCommand : AsyncCommand<McpResourcesCommand.Settings>
{
    private readonly IUserPreferencesStore _store;
    private readonly IConnectionService _connections;

    public McpResourcesCommand(IUserPreferencesStore store, IConnectionService connections)
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
            var resources = conn.Resources;
            if (resources.Count == 0)
            {
                AnsiConsole.MarkupLine("[dim]No resources found.[/]");
                return 0;
            }

            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("URI")
                .AddColumn("Name")
                .AddColumn("Description")
                .AddColumn("MIME Type");

            foreach (var r in resources.OrderBy(r => r.Uri))
            {
                table.AddRow(
                    Markup.Escape(r.Uri),
                    Markup.Escape(r.Name),
                    Markup.Escape(string.IsNullOrWhiteSpace(r.Description) ? "—" : r.Description),
                    Markup.Escape(string.IsNullOrWhiteSpace(r.MimeType) ? "—" : r.MimeType)
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
