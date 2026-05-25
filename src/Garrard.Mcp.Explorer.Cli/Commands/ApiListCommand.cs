using Garrard.Mcp.Explorer.Core.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Garrard.Mcp.Explorer.Cli.Commands;

public sealed class ApiListCommand : AsyncCommand
{
    private readonly IHttpApiStore _store;

    public ApiListCommand(IHttpApiStore store) => _store = store;

    protected override async Task<int> ExecuteAsync(CommandContext context, CancellationToken ct)
    {
        var defs = await _store.GetAllDefinitionsAsync();
        var favs = await _store.GetFavouriteIdsAsync();
        var favSet = new HashSet<string>(favs, StringComparer.OrdinalIgnoreCase);

        if (defs.Count == 0) { AnsiConsole.MarkupLine("[dim]No HTTP API definitions found.[/]"); return 0; }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("★")
            .AddColumn("Name")
            .AddColumn("Method")
            .AddColumn("URL")
            .AddColumn("Auth")
            .AddColumn("Group");

        foreach (var d in defs.OrderBy(d => d.Name))
        {
            table.AddRow(
                favSet.Contains(d.Id) ? "[yellow]★[/]" : " ",
                Markup.Escape(d.Name),
                d.Method,
                Markup.Escape($"{d.BaseUrl}{d.Path}"),
                d.AuthenticationMode.ToString(),
                Markup.Escape(d.GroupName ?? "—")
            );
        }

        AnsiConsole.Write(table);
        return 0;
    }
}
