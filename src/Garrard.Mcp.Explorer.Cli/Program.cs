using Garrard.Mcp.Explorer.Cli;
using Garrard.Mcp.Explorer.Cli.Commands;
using Garrard.Mcp.Explorer.Infrastructure.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Cli.Help;

// ── Pre-parse --data-path before Spectre.Console sees it ─────────────────────
// Usage: mcp-http --data-path "/path/to/McpExplorerv2" http run-collection …
// Strips the option from args so Spectre.Console doesn't error on an unknown flag.
var argsList = args.ToList();
var originalArgsEmpty = args.Length == 0;
string? dataPathOverride = null;
var dpIdx = argsList.IndexOf("--data-path");
if (dpIdx >= 0 && dpIdx + 1 < argsList.Count)
{
    dataPathOverride = argsList[dpIdx + 1];
    argsList.RemoveRange(dpIdx, 2);
    args = [.. argsList];
}

// ── Banner (shown on --help, --version, or no args) ──────────────────────────
var version = typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
var showBanner = originalArgsEmpty || args.Any(a => a is "--help" or "-h" or "--version" or "-v");
if (showBanner && !Console.IsOutputRedirected)
{
    AnsiConsole.Write(new FigletText("mcp-http").Color(Color.GreenYellow));
    //https://spectreconsole.net/console/reference/color-reference
    AnsiConsole.MarkupLine($"version: [bold orange1]{version}[/] [gray82]MCP Explorer CLI[/]\n");
}

// ── Dependency injection container ───────────────────────────────────────────
var configBuilder = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.cli.json", optional: true)
    .AddEnvironmentVariables();

if (!string.IsNullOrWhiteSpace(dataPathOverride))
{
    configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["PREFERENCES:StoragePath"] = Path.Combine(dataPathOverride, "settings.json")
    });
}

var configuration = configBuilder.Build();

var services = new ServiceCollection();
services.AddSingleton<IConfiguration>(configuration);
services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
services.AddMcpExplorerInfrastructure(configuration);

var registrar = new TypeRegistrar(services);

// ── CLI app ───────────────────────────────────────────────────────────────────
var app = new CommandApp(registrar);

app.Configure(config =>
{
    config.SetApplicationName("mcp-http");
    config.SetApplicationVersion(version);
    // Note: --data-path <dir> is a global pre-option processed before Spectre.Console.
    // It overrides the data directory (equivalent to MCP_DATA_PATH used by Docker).
    config.Settings.HelpProviderStyles = new HelpProviderStyle
    {
        Description = new DescriptionStyle
        {
            Header = "bold"
        },
        Usage = new UsageStyle
        {
            Header = "bold",
            CurrentCommand = "bold",
            Command = "bold",
            Options = "bold",
            RequiredArgument = "bold",
            OptionalArgument = "bold"
        },
        Examples = new ExampleStyle
        {
            Header = "bold",
            Arguments = "bold"
        },
        Arguments = new ArgumentStyle
        {
            Header = "bold",
            RequiredArgument = "bold",
            OptionalArgument = "bold"
        },
        Options = new OptionStyle
        {
            Header = "bold",
            DefaultValueHeader = "bold",
            DefaultValue = "bold",
            RequiredOption = "bold",
            RequiredOptionValue = "bold",
            OptionalOptionValue = "bold"
        },
        Commands = new CommandStyle
        {
            Header = "bold",
            ChildCommand = "bold",
            RequiredArgument = "bold"
        }
    };

    config.AddBranch("http", http =>
    {
        http.SetDescription("HTTP API Explorer commands");

        http.AddBranch("api", api =>
        {
            api.SetDescription("HTTP API definition commands");

            api.AddCommand<ApiListCommand>("list")
                .WithDescription("List all saved HTTP API definitions.")
                .WithExample("http", "api", "list");

            api.AddCommand<HttpInvokeCommand>("invoke")
                .WithDescription("Invoke an HTTP API endpoint by name or ID and display the response with inferred schema.")
                .WithExample("http", "api", "invoke", "--name", "My API")
                .WithExample("http", "api", "invoke", "--name", "My API", "--use-localhost")
                .WithExample("--data-path", "/path/to/data", "http", "api", "invoke", "--name", "My API");

            api.AddCommand<HttpCompareCommand>("compare")
                .WithDescription("Invoke an endpoint and compare the response schema against its saved baseline snapshot.")
                .WithExample("http", "api", "compare", "--name", "My API", "--fail-on-breaking");

            api.AddCommand<HttpHistoryCommand>("history")
                .WithDescription("Show the recent invocation history for an endpoint or all endpoints.")
                .WithExample("http", "api", "history", "--endpoint", "My API", "--limit", "20");

            api.AddCommand<ApiExportCommand>("export")
                .WithDescription("Export selected API definitions to an encrypted file.")
                .WithExample("http", "api", "export", "--output", "export.json", "--password", "secret");

            api.AddCommand<ApiImportCommand>("import")
                .WithDescription("Import API definitions from an encrypted export file.")
                .WithExample("http", "api", "import", "--file", "export.json", "--password", "secret");
        });

        http.AddBranch("collection", collection =>
        {
            collection.SetDescription("HTTP API collection commands");

            collection.AddCommand<HttpListCollectionsCommand>("list")
                .WithDescription("List all saved collections.")
                .WithExample("http", "collection", "list");

            collection.AddCommand<HttpRunCollectionCommand>("run")
                .WithDescription("Run all endpoints in a collection, compare against baselines, and print a summary table.")
                .WithExample("http", "collection", "run", "--name", "Regression Suite", "--fail-on-breaking")
                .WithExample("http", "collection", "run", "--name", "Regression Suite", "--use-localhost", "--fail-on-breaking")
                .WithExample("--data-path", "/path/to/data", "http", "collection", "run", "--name", "Regression Suite", "--fail-on-breaking");
        });
    });

    config.AddBranch("mcp", mcp =>
    {
        mcp.SetDescription("MCP connection commands");

        mcp.AddCommand<McpConnectionsCommand>("connections")
            .WithDescription("List all saved MCP connections.")
            .WithExample("mcp", "connections");

        mcp.AddCommand<McpToolsCommand>("tools")
            .WithDescription("Connect to an MCP server and list its available tools.")
            .WithExample("mcp", "tools", "--name", "My Server");

        mcp.AddCommand<McpResourcesCommand>("resources")
            .WithDescription("Connect to an MCP server and list its available resources.")
            .WithExample("mcp", "resources", "--name", "My Server");

        mcp.AddCommand<McpPromptsCommand>("prompts")
            .WithDescription("Connect to an MCP server and list its available prompts.")
            .WithExample("mcp", "prompts", "--name", "My Server");

        mcp.AddCommand<McpTemplatesCommand>("templates")
            .WithDescription("Connect to an MCP server and list its available resource templates.")
            .WithExample("mcp", "templates", "--name", "My Server");

        mcp.AddCommand<McpInvokeCommand>("invoke")
            .WithDescription("Connect to an MCP server and invoke a tool.")
            .WithExample("mcp", "invoke", "--name", "My Server", "--tool", "echo", "--param", "message=hello")
            .WithExample("mcp", "invoke", "--name", "My Server", "--tool", "search", "--params", "{\"query\":\"dotnet\"}");
    });
});

return await app.RunAsync(args);
