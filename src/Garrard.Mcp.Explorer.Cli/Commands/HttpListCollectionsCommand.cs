using Garrard.Mcp.Explorer.Core.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Garrard.Mcp.Explorer.Cli.Commands;

public sealed class HttpListCollectionsCommand : AsyncCommand
{
    private readonly IHttpApiStore _store;

    public HttpListCollectionsCommand(IHttpApiStore store) => _store = store;

    protected override async Task<int> ExecuteAsync(CommandContext context, CancellationToken ct)
    {
        var collections = await _store.GetAllCollectionsAsync(ct);

        if (collections.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim]No collections found.[/]");
            return 0;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Name")
            .AddColumn("Description")
            .AddColumn("Endpoints")
            .AddColumn("Group")
            .AddColumn("Last Run");

        foreach (var c in collections.OrderBy(c => c.Name))
        {
            table.AddRow(
                Markup.Escape(c.Name),
                Markup.Escape(string.IsNullOrWhiteSpace(c.Description) ? "—" : c.Description),
                c.EndpointIds.Count.ToString(),
                Markup.Escape(c.GroupName ?? "—"),
                c.LastRunAt.HasValue ? Markup.Escape(c.LastRunAt.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm")) : "[dim]Never[/]"
            );
        }

        AnsiConsole.Write(table);
        return 0;
    }
}
