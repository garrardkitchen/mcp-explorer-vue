using Garrard.Mcp.Explorer.Cli;
using Garrard.Mcp.Explorer.Cli.Commands;
using Garrard.Mcp.Explorer.Infrastructure.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console.Cli;

// ── Dependency injection container ───────────────────────────────────────────
var configBuilder = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.cli.json", optional: true)
    .AddEnvironmentVariables();

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
    config.SetApplicationVersion("1.0.0");

    config.AddBranch("http", http =>
    {
        http.SetDescription("HTTP API Explorer commands");

        http.AddCommand<HttpInvokeCommand>("invoke")
            .WithDescription("Invoke an HTTP API endpoint by name or ID and display the response with inferred schema.")
            .WithExample("http", "invoke", "--name", "My API");

        http.AddCommand<HttpCompareCommand>("compare")
            .WithDescription("Invoke an endpoint and compare the response schema against its saved baseline snapshot.")
            .WithExample("http", "compare", "--name", "My API", "--fail-on-breaking");

        http.AddCommand<HttpRunCollectionCommand>("run-collection")
            .WithDescription("Run all endpoints in a collection, compare against baselines, and print a summary table.")
            .WithExample("http", "run-collection", "--name", "Regression Suite", "--fail-on-breaking");

        http.AddCommand<HttpHistoryCommand>("history")
            .WithDescription("Show the recent invocation history for an endpoint or all endpoints.")
            .WithExample("http", "history", "--endpoint", "My API", "--limit", "20");
    });

    config.AddBranch("apis", apis =>
    {
        apis.SetDescription("HTTP API definition management commands");

        apis.AddCommand<ApiListCommand>("list")
            .WithDescription("List all saved HTTP API definitions.");

        apis.AddCommand<ApiExportCommand>("export")
            .WithDescription("Export selected API definitions to an encrypted file.")
            .WithExample("apis", "export", "--output", "export.json", "--password", "secret");

        apis.AddCommand<ApiImportCommand>("import")
            .WithDescription("Import API definitions from an encrypted export file.")
            .WithExample("apis", "import", "--file", "export.json", "--password", "secret");
    });
});

return await app.RunAsync(args);
