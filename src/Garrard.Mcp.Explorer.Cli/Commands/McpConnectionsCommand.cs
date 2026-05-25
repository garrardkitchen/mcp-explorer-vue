using Garrard.Mcp.Explorer.Core.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Garrard.Mcp.Explorer.Cli.Commands;

public sealed class McpConnectionsCommand : AsyncCommand
{
    private readonly IUserPreferencesStore _store;

    public McpConnectionsCommand(IUserPreferencesStore store) => _store = store;

    protected override async Task<int> ExecuteAsync(CommandContext context, CancellationToken ct)
    {
        var prefs = await _store.LoadAsync(ct);
        var connections = prefs.Connections;

        if (connections.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim]No MCP connections found.[/]");
            return 0;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Name")
            .AddColumn("Endpoint")
            .AddColumn("Auth")
            .AddColumn("Group")
            .AddColumn("Created");

        foreach (var c in connections.OrderBy(c => c.Name))
        {
            table.AddRow(
                Markup.Escape(c.Name),
                Markup.Escape(c.Endpoint),
                c.AuthenticationMode.ToString(),
                Markup.Escape(c.GroupName ?? "—"),
                c.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
            );
        }

        AnsiConsole.Write(table);
        return 0;
    }
}
