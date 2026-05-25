using Garrard.Mcp.Explorer.Core.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Text.Json;

namespace Garrard.Mcp.Explorer.Cli.Commands;

public sealed class McpInvokeCommand : AsyncCommand<McpInvokeCommand.Settings>
{
    private readonly IUserPreferencesStore _store;
    private readonly IConnectionService _connections;

    public McpInvokeCommand(IUserPreferencesStore store, IConnectionService connections)
    {
        _store = store;
        _connections = connections;
    }

    public sealed class Settings : CommandSettings
    {
        [CommandOption("--name")]
        [Description("MCP connection name (exact or partial match)")]
        public string? Name { get; init; }

        [CommandOption("--tool")]
        [Description("Tool name to invoke")]
        public string? Tool { get; init; }

        [CommandOption("--params")]
        [Description("Tool parameters as a JSON object string (e.g. '{\"key\":\"value\"}')")]
        public string? ParamsJson { get; init; }

        [CommandOption("--param")]
        [Description("Tool parameter as key=value (repeatable; merged with --params, last writer wins)")]
        public string[]? Param { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(settings.Name))
        {
            AnsiConsole.MarkupLine("[red]Specify --name <connection_name>[/]");
            return 1;
        }
        if (string.IsNullOrWhiteSpace(settings.Tool))
        {
            AnsiConsole.MarkupLine("[red]Specify --tool <tool_name>[/]");
            return 1;
        }

        var prefs = await _store.LoadAsync(ct);
        var def = McpCommandHelper.ResolveConnection(prefs, settings.Name);
        if (def is null) return 1;

        McpCommandHelper.WarnIfOAuth(def);

        var parameters = new Dictionary<string, object?>();

        if (!string.IsNullOrWhiteSpace(settings.ParamsJson))
        {
            try
            {
                var fromJson = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(settings.ParamsJson);
                if (fromJson is not null)
                    foreach (var (k, v) in fromJson)
                        parameters[k] = ParseJsonElement(v);
            }
            catch (JsonException ex)
            {
                AnsiConsole.MarkupLine($"[red]Invalid --params JSON: {Markup.Escape(ex.Message)}[/]");
                return 1;
            }
        }

        foreach (var p in settings.Param ?? [])
        {
            var idx = p.IndexOf('=');
            if (idx < 0)
            {
                AnsiConsole.MarkupLine($"[red]Invalid --param '[bold]{Markup.Escape(p)}[/]'. Expected key=value.[/]");
                return 1;
            }
            var key = p[..idx];
            if (string.IsNullOrWhiteSpace(key))
            {
                AnsiConsole.MarkupLine($"[red]Invalid --param '[bold]{Markup.Escape(p)}[/]': key cannot be empty.[/]");
                return 1;
            }
            var raw = p[(idx + 1)..];
            parameters[key] = TryParseAsJson(raw) ?? (object?)raw;
        }

        string result = string.Empty;
        try
        {
            await AnsiConsole.Status().StartAsync(
                $"Connecting to [bold]{Markup.Escape(def.Name)}[/] and invoking [bold]{Markup.Escape(settings.Tool!)}[/]…",
                async _ =>
                {
                    await _connections.ConnectAsync(def, ct);
                    result = await _connections.InvokeToolAsync(def.Name, settings.Tool!, parameters, ct);
                });
        }
        finally
        {
            try { await _connections.DisconnectAsync(def.Name, CancellationToken.None); } catch { /* best-effort cleanup */ }
        }

        AnsiConsole.Write(new Panel(new Text(result))
            .Header($"Result: {settings.Tool}")
            .Border(BoxBorder.Rounded));

        return 0;
    }

    /// <summary>Extracts a native .NET value from a <see cref="JsonElement"/>.</summary>
    private static object? ParseJsonElement(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number when element.TryGetInt64(out var i) => i,
        JsonValueKind.Number => element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        _ => element // keep arrays/objects as JsonElement
    };

    /// <summary>
    /// Tries to parse <paramref name="raw"/> as a JSON literal (bool, number, array, object).
    /// Returns null if the value should be treated as a plain string.
    /// </summary>
    private static object? TryParseAsJson(string raw)
    {
        if (raw.Length == 0) return null;
        var first = raw[0];
        if (first is '{' or '[' || raw is "true" or "false" or "null")
        {
            try { return JsonSerializer.Deserialize<JsonElement>(raw); } catch { }
        }
        if (first is '-' || char.IsDigit(first))
        {
            try { return JsonSerializer.Deserialize<JsonElement>(raw); } catch { }
        }
        return null;
    }
}
