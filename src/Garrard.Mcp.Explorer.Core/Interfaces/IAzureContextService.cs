using Garrard.Mcp.Explorer.Core.Domain.Connections;

namespace Garrard.Mcp.Explorer.Core.Interfaces;

/// <summary>
/// Provides Azure context information (account, app registrations, Key Vault inventory)
/// by querying the Azure control plane via <c>DefaultAzureCredential</c>.
/// The credential chain is: AzureCliCredential → EnvironmentCredential → VisualStudioCredential.
/// </summary>
public interface IAzureContextService
{
    /// <summary>Returns the active Azure account information from the current credential context.</summary>
    Task<AzureAccountInfo?> GetAccountInfoAsync(CancellationToken cancellationToken = default);

    /// <summary>Lists all subscriptions accessible to the current credential.</summary>
    Task<IReadOnlyList<AzureSubscription>> GetSubscriptionsAsync(CancellationToken cancellationToken = default);

    /// <summary>Lists all app registrations visible to the current credential (tenant-scoped).</summary>
    Task<IReadOnlyList<AzureAppRegistration>> GetAppRegistrationsAsync(CancellationToken cancellationToken = default);

    /// <summary>Lists all Key Vaults in the specified subscription (or the default if null).</summary>
    Task<IReadOnlyList<AzureKeyVaultInfo>> GetKeyVaultsAsync(string? subscriptionId = null, CancellationToken cancellationToken = default);

    /// <summary>Lists all secret names in the specified Key Vault.</summary>
    Task<IReadOnlyList<string>> GetKeyVaultSecretNamesAsync(string vaultName, CancellationToken cancellationToken = default);
}

/// <summary>Current Azure account / subscription context.</summary>
public sealed record AzureAccountInfo(
    string TenantId,
    string SubscriptionId,
    string SubscriptionName,
    string UserPrincipalName,
    string? Location);

/// <summary>An Azure subscription accessible to the current credential.</summary>
public sealed record AzureSubscription(
    string Id,
    string Name,
    string TenantId,
    bool IsDefault);

/// <summary>Summary of an Azure AD App Registration.</summary>
public sealed record AzureAppRegistration(
    string AppId,
    string DisplayName,
    /// <summary>ResourceAppId of the first API permission — used to derive the default scope.</summary>
    string? FirstApiResourceId);

/// <summary>Summary of an Azure Key Vault resource.</summary>
public sealed record AzureKeyVaultInfo(
    string Name,
    string? ResourceGroup,
    string? Location);
