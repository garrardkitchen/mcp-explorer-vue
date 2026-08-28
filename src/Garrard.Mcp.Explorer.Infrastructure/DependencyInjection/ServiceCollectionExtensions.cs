using Garrard.Mcp.Explorer.Core.Interfaces;
using Garrard.Mcp.Explorer.Infrastructure.Azure;
using Garrard.Mcp.Explorer.Infrastructure.Certificates;
using Garrard.Mcp.Explorer.Infrastructure.Connections;
using Garrard.Mcp.Explorer.Infrastructure.DevTunnels;
using Garrard.Mcp.Explorer.Infrastructure.Elicitation;
using Garrard.Mcp.Explorer.Infrastructure.HttpApi;
using Garrard.Mcp.Explorer.Infrastructure.LlmProviders;
using Garrard.Mcp.Explorer.Infrastructure.Mcp;
using Garrard.Mcp.Explorer.Infrastructure.Persistence;
using Garrard.Mcp.Explorer.Infrastructure.Security;
using Garrard.Mcp.Explorer.Infrastructure.Workflows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
        services.AddSingleton<ISecretProtector>(sp => new SecretProtector(keyDirectory, sp.GetService<ILogger<SecretProtector>>()));

        services.AddSingleton<IUserPreferencesStore>(sp =>
            new UserPreferencesStore(sp.GetRequiredService<ISecretProtector>(), string.IsNullOrWhiteSpace(customPath) ? null : customPath));

        // MCP connections
        services.AddSingleton<OAuthCallbackService>();
        services.AddScoped<IConnectionExportService, ConnectionExportService>();
        services.AddSingleton<ElicitationService>();
        services.AddSingleton<IElicitationService>(sp => sp.GetRequiredService<ElicitationService>());
        services.AddHttpClient();
        services.AddHttpClient("ChatDocumentPreview", client => client.Timeout = TimeSpan.FromSeconds(60))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AllowAutoRedirect = false
            });
        services.AddHttpClient("DevTunnelReplay", client => client.Timeout = TimeSpan.FromSeconds(30));

        services.AddSingleton<IDevTunnelCli, DevTunnelCli>();
        services.AddSingleton<IWebhookEventStore, JsonlWebhookEventStore>();
        services.AddSingleton<DevTunnelService>();
        services.AddSingleton<IDevTunnelService>(sp => sp.GetRequiredService<DevTunnelService>());
        services.AddHostedService<TunnelSupervisor>();

        // Azure context & Key Vault
        services.AddSingleton<IKeyVaultSecretResolver, KeyVaultSecretResolver>();
        services.AddSingleton<IAzureContextService, AzureContextService>();

        // Certificates — the store lives alongside settings.json (certs/ subfolder)
        services.AddSingleton<ICertificateService>(sp => new CertificateService(
            string.IsNullOrWhiteSpace(customPath) ? null : customPath,
            sp.GetRequiredService<IUserPreferencesStore>(),
            sp.GetRequiredService<IHttpApiStore>(),
            sp.GetService<ILogger<CertificateService>>()));
        services.AddSingleton<ICertificateUploadService, GraphKeyCredentialService>();
        services.AddSingleton<ICertificateRenewalService, CertificateRenewalService>();
        services.AddSingleton<IKeyVaultCertificateService, KeyVaultCertificateService>();
        services.AddSingleton<CertificateNotificationState>();
        services.AddHostedService<CertificateExpiryMonitor>();

        services.AddSingleton<ConnectionService>();
        services.AddSingleton<IConnectionService>(sp => sp.GetRequiredService<ConnectionService>());
        services.AddHostedService<ConnectionUpdateService>();

        // LLM providers
        services.AddSingleton<IFoundryToolApprovalService, FoundryToolApprovalService>();
        services.AddHttpClient("FoundryProjectAgentDiscovery", client =>
            client.Timeout = TimeSpan.FromSeconds(30));
        services.AddScoped<IFoundryProjectAgentService, FoundryProjectAgentService>();
        services.AddScoped<IAiChatService, AiChatService>();
        services.AddScoped<ILlmExecutionService, LlmExecutionService>();

        // Workflows
        services.AddScoped<IWorkflowService, WorkflowService>();
        services.AddScoped<LoadTestService>();

        // HTTP API Explorer
        services.AddSingleton<IHttpApiStore, HttpApiStore>();
        services.AddSingleton<IHttpApiSnapshotStore, JsonlHttpApiSnapshotStore>();
        services.AddSingleton<ISchemaInferenceService, SchemaInferenceService>();
        services.AddSingleton<ISchemaComparisonService, SchemaComparisonService>();
        services.AddSingleton<IHttpApiExportService, HttpApiExportService>();
        services.AddHttpClient("HttpApiInvoker", client => client.Timeout = TimeSpan.FromSeconds(30))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                // Redirects are handled explicitly in HttpApiInvoker so the app can
                // stop before HTTP endpoints silently upgrade into HTTPS.
                AllowAutoRedirect = false
            });
        services.AddScoped<IHttpApiInvoker, HttpApiInvoker>();

        return services;
    }
}
