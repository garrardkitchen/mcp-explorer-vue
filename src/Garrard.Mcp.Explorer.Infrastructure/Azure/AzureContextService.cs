using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.KeyVault;
using Azure.ResourceManager.Resources;
using Azure.Security.KeyVault.Secrets;
using Garrard.Mcp.Explorer.Core.Interfaces;
using Microsoft.Graph;
using Microsoft.Extensions.Logging;

namespace Garrard.Mcp.Explorer.Infrastructure.Azure;

/// <summary>
/// Queries the Azure control plane to provide contextual information for the
/// connection form: active account, app registrations, and Key Vault inventory.
/// Uses <c>DefaultAzureCredential</c> with the same chain as <see cref="KeyVaultSecretResolver"/>.
/// </summary>
public sealed class AzureContextService : IAzureContextService
{
    private readonly DefaultAzureCredential _credential;
    private readonly ILogger<AzureContextService> _logger;

    public AzureContextService(ILogger<AzureContextService> logger)
    {
        _logger = logger;
        _credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
        {
            ExcludeManagedIdentityCredential = false,
            ExcludeAzureCliCredential = false,
            ExcludeEnvironmentCredential = false,
            ExcludeVisualStudioCredential = false,
            ExcludeInteractiveBrowserCredential = true,
            ExcludeAzurePowerShellCredential = true,
            ExcludeWorkloadIdentityCredential = false,
        });
    }

    /// <inheritdoc/>
    public async Task<AzureAccountInfo?> GetAccountInfoAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var armClient = new ArmClient(_credential);
            var subscription = await armClient.GetDefaultSubscriptionAsync(cancellationToken).ConfigureAwait(false);
            var data = subscription.Data;

            // Tenant ID comes from the subscription's tenant
            var tenantId = data.TenantId?.ToString() ?? string.Empty;

            // Try to get the current user principal name via Graph
            string upn = string.Empty;
            try
            {
                var graphClient = new GraphServiceClient(_credential, ["https://graph.microsoft.com/.default"]);
                var me = await graphClient.Me.GetAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
                upn = me?.UserPrincipalName ?? me?.DisplayName ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not retrieve current user from Graph (service principal context)");
            }

            return new AzureAccountInfo(
                TenantId: tenantId,
                SubscriptionId: data.SubscriptionId ?? string.Empty,
                SubscriptionName: data.DisplayName ?? string.Empty,
                UserPrincipalName: upn,
                Location: data.Id?.Location ?? null);
        }
        catch (Exception ex)
        {
            LogAzureFailure(ex, "Could not retrieve Azure account info");
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AzureAppRegistration>> GetAppRegistrationsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var graphClient = new GraphServiceClient(_credential, ["https://graph.microsoft.com/.default"]);
            var apps = await graphClient.Applications.GetAsync(req =>
            {
                req.QueryParameters.Select = ["appId", "displayName", "requiredResourceAccess"];
                req.QueryParameters.Top = 999;
            }, cancellationToken).ConfigureAwait(false);

            var result = new List<AzureAppRegistration>();
            var page = apps;
            while (page?.Value is not null)
            {
                foreach (var app in page.Value)
                {
                    var firstApiResourceId = app.RequiredResourceAccess?
                        .FirstOrDefault(r => r.ResourceAppId?.ToString() is { Length: > 0 })
                        ?.ResourceAppId?.ToString();

                    result.Add(new AzureAppRegistration(
                        AppId: app.AppId ?? string.Empty,
                        DisplayName: app.DisplayName ?? string.Empty,
                        FirstApiResourceId: firstApiResourceId));
                }

                if (page.OdataNextLink is null) break;
                page = await graphClient.Applications
                    .WithUrl(page.OdataNextLink)
                    .GetAsync(cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }

            return result.OrderBy(a => a.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
        }
        catch (Exception ex)
        {
            LogAzureFailure(ex, "Could not retrieve App Registrations from Microsoft Graph");
            return [];
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AzureSubscription>> GetSubscriptionsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var armClient = new ArmClient(_credential);
            var result = new List<AzureSubscription>();
            await foreach (var sub in armClient.GetSubscriptions().GetAllAsync(cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                result.Add(new AzureSubscription(
                    Id: sub.Data.SubscriptionId ?? string.Empty,
                    Name: sub.Data.DisplayName ?? string.Empty,
                    TenantId: sub.Data.TenantId?.ToString() ?? string.Empty,
                    IsDefault: false));
            }

            // Mark the default subscription from the account profile
            var accountInfo = await GetAccountInfoAsync(cancellationToken).ConfigureAwait(false);
            if (accountInfo is not null)
            {
                for (var i = 0; i < result.Count; i++)
                {
                    if (result[i].Id == accountInfo.SubscriptionId)
                        result[i] = result[i] with { IsDefault = true };
                }
            }

            return result.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }
        catch (Exception ex)
        {
            LogAzureFailure(ex, "Could not retrieve Azure subscriptions");
            return [];
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AzureKeyVaultInfo>> GetKeyVaultsAsync(string? subscriptionId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var armClient = new ArmClient(_credential);
            var subscription = subscriptionId is { Length: > 0 }
                ? armClient.GetSubscriptionResource(SubscriptionResource.CreateResourceIdentifier(subscriptionId))
                : await armClient.GetDefaultSubscriptionAsync(cancellationToken).ConfigureAwait(false);

            var vaults = new List<AzureKeyVaultInfo>();
            await foreach (var vault in subscription.GetKeyVaultsAsync(cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                var resourceGroup = vault.Id?.ResourceGroupName;
                vaults.Add(new AzureKeyVaultInfo(
                    Name: vault.Data.Name,
                    ResourceGroup: resourceGroup,
                    Location: vault.Data.Location));
            }

            return vaults.OrderBy(v => v.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }
        catch (Exception ex)
        {
            LogAzureFailure(ex, "Could not retrieve Key Vaults from Azure Resource Manager");
            return [];
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> GetKeyVaultSecretNamesAsync(string vaultName, CancellationToken cancellationToken = default)
    {
        try
        {
            var vaultUri = new Uri($"https://{vaultName}.vault.azure.net/");
            var secretClient = new SecretClient(vaultUri, _credential);

            var secrets = new List<string>();
            await foreach (var secretProps in secretClient.GetPropertiesOfSecretsAsync(cancellationToken).ConfigureAwait(false))
            {
                if (secretProps.Enabled != false)
                    secrets.Add(secretProps.Name);
            }

            return secrets.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList();
        }
        catch (Exception ex)
        {
            LogAzureFailure(ex, "Could not retrieve secrets from Key Vault '{vaultName}'", vaultName);
            return [];
        }
    }

    /// <summary>
    /// Logs Azure credential failures at <c>Debug</c> (expected when no credentials are configured)
    /// and all other failures at <c>Warning</c> (unexpected).
    /// This prevents the full <see cref="CredentialUnavailableException"/> chain from cluttering
    /// production logs when the app is running without Azure credentials.
    /// </summary>
    private void LogAzureFailure(Exception ex, string message, params object?[] args)
    {
        if (ex is CredentialUnavailableException)
            _logger.LogDebug("Azure credentials unavailable — {Message}: {Reason}", message, ex.Message);
        else
            _logger.LogWarning(ex, message, args);
    }
}
