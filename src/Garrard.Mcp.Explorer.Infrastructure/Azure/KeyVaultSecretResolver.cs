using System.Text.RegularExpressions;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Garrard.Mcp.Explorer.Core.Domain.Connections;
using Garrard.Mcp.Explorer.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Garrard.Mcp.Explorer.Infrastructure.Azure;

/// <summary>
/// Resolves Azure Key Vault secret references at runtime using <c>DefaultAzureCredential</c>.
/// Credential chain order: <c>AzureCliCredential</c> → <c>EnvironmentCredential</c> → <c>VisualStudioCredential</c>.
/// </summary>
public sealed class KeyVaultSecretResolver : IKeyVaultSecretResolver
{
    // Defence-in-depth: reject vault names that don't match Azure naming rules before building the URI.
    private static readonly Regex VaultNamePattern = new(@"^[a-zA-Z][a-zA-Z0-9\-]{1,22}[a-zA-Z0-9]$", RegexOptions.Compiled);

    private readonly DefaultAzureCredential _credential;
    private readonly ILogger<KeyVaultSecretResolver> _logger;

    public KeyVaultSecretResolver(ILogger<KeyVaultSecretResolver> logger)
    {
        _logger = logger;

        // Explicit chain: AzureCLI first (local dev), then environment (containers/CI),
        // then Visual Studio (IDE dev). Excludes managed identity to keep resolution
        // predictable during local development.
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
    public async Task<string> ResolveAsync(KeyVaultSecretReference reference, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference.VaultName);
        ArgumentException.ThrowIfNullOrWhiteSpace(reference.SecretName);

        if (!VaultNamePattern.IsMatch(reference.VaultName))
            throw new ArgumentException(
                $"Vault name '{reference.VaultName}' does not conform to Azure Key Vault naming rules (3-24 chars, alphanumeric/hyphens).",
                nameof(reference));

        var vaultUri = new Uri($"https://{reference.VaultName}.vault.azure.net/");
        _logger.LogDebug("Resolving Key Vault secret: {Vault}/{Secret}", reference.VaultName, reference.SecretName);

        try
        {
            var client = new SecretClient(vaultUri, _credential);
            var response = await client.GetSecretAsync(reference.SecretName, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return response.Value.Value
                ?? throw new InvalidOperationException($"Key Vault secret '{reference}' has a null value.");
        }
        catch (OperationCanceledException)
        {
            // Propagate cancellation without wrapping so callers can observe it correctly.
            throw;
        }
        catch (CredentialUnavailableException ex)
        {
            // Expected when no Azure credentials are configured.
            _logger.LogDebug("Azure credentials unavailable resolving {Reference}: {Reason}", reference, ex.Message);
            throw new InvalidOperationException(
                $"Could not resolve Key Vault secret '{reference}': Azure credentials are not configured. Run 'az login' or set environment credentials.",
                ex);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "Failed to resolve Key Vault secret {Reference}", reference);
            throw new InvalidOperationException(
                $"Could not resolve Key Vault secret '{reference}'. Ensure DefaultAzureCredential is configured and the vault is accessible.",
                ex);
        }
    }
}
