using Garrard.Mcp.Explorer.Core.Domain.Connections;
using Garrard.Mcp.Explorer.Core.Domain.Preferences;
using Spectre.Console;

namespace Garrard.Mcp.Explorer.Cli.Commands;

internal static class McpCommandHelper
{
    /// <summary>
    /// Resolves a <see cref="ConnectionDefinition"/> by exact name first, then by a single
    /// partial case-insensitive match. Prints an error and returns null when ambiguous or missing.
    /// </summary>
    internal static ConnectionDefinition? ResolveConnection(UserPreferences prefs, string name)
    {
        var exact = prefs.Connections.FirstOrDefault(c =>
            string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
        if (exact is not null) return exact;

        var partial = prefs.Connections
            .Where(c => c.Name?.Contains(name, StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        if (partial.Count == 1) return partial[0];

        if (partial.Count > 1)
        {
            AnsiConsole.MarkupLine($"[red]Multiple connections match '[bold]{Markup.Escape(name)}[/]'. Use a more exact name:[/]");
            foreach (var c in partial)
                AnsiConsole.MarkupLine($"  • {Markup.Escape(c.Name)}");
            return null;
        }

        AnsiConsole.MarkupLine($"[red]No connection matching '[bold]{Markup.Escape(name)}[/]'.[/]");
        return null;
    }

    /// <summary>Prints a warning if the connection uses OAuth, which may not work from the CLI.</summary>
    internal static void WarnIfOAuth(ConnectionDefinition def)
    {
        if (def.AuthenticationMode == ConnectionAuthenticationMode.OAuth)
            AnsiConsole.MarkupLine("[yellow]⚠ This connection uses OAuth. A running API callback endpoint is required — the connection may fail from the CLI.[/]");
    }
}
