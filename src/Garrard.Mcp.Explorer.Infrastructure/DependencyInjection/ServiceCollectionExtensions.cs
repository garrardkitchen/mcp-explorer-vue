using Garrard.Mcp.Explorer.Core.Interfaces;
using Garrard.Mcp.Explorer.Infrastructure.Azure;
using Garrard.Mcp.Explorer.Infrastructure.Connections;
using Garrard.Mcp.Explorer.Infrastructure.Elicitation;
using Garrard.Mcp.Explorer.Infrastructure.LlmProviders;
using Garrard.Mcp.Explorer.Infrastructure.Mcp;
using Garrard.Mcp.Explorer.Infrastructure.Persistence;
using Garrard.Mcp.Explorer.Infrastructure.Security;
using Garrard.Mcp.Explorer.Infrastructure.Workflows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Garrard.Mcp.Explorer.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMcpExplorerInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // PREFERENCES__StoragePath env var is normalised to PREFERENCES:StoragePath by ASP.NET Core
        var customPath = configuration["PREFERENCES:StoragePath"]
                         ?? Environment.GetEnvironmentVariable("PREFERENCES__StoragePath");

        // Derive the key directory from the settings file path so the same mounted volume
        // supplies both settings.json and secret.key (critical for Docker container deployments).
        var keyDirectory = string.IsNullOrWhiteSpace(customPath)
            ? null
            : Path.GetDirectoryName(customPath);

        // Security — must be registered before UserPreferencesStore which depends on it
        services.AddSingleton<ISecretProtector>(_ => new SecretProtector(keyDirectory));

        services.AddSingleton<IUserPreferencesStore>(sp =>
            new UserPreferencesStore(sp.GetRequiredService<ISecretProtector>(), string.IsNullOrWhiteSpace(customPath) ? null : customPath));

        // MCP connections
        services.AddSingleton<OAuthCallbackService>();
        services.AddScoped<IConnectionExportService, ConnectionExportService>();
        services.AddSingleton<ElicitationService>();
        services.AddSingleton<IElicitationService>(sp => sp.GetRequiredService<ElicitationService>());

        // Azure context & Key Vault
        services.AddSingleton<IKeyVaultSecretResolver, KeyVaultSecretResolver>();
        services.AddSingleton<IAzureContextService, AzureContextService>();

        services.AddSingleton<ConnectionService>();
        services.AddSingleton<IConnectionService>(sp => sp.GetRequiredService<ConnectionService>());
        services.AddHostedService<ConnectionUpdateService>();

        // LLM providers
        services.AddScoped<IAiChatService, AiChatService>();
        services.AddScoped<ILlmExecutionService, LlmExecutionService>();

        // Workflows
        services.AddScoped<IWorkflowService, WorkflowService>();
        services.AddScoped<LoadTestService>();

        return services;
    }
}

