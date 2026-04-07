using System.Text.RegularExpressions;
using Asp.Versioning;
using Garrard.Mcp.Explorer.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Garrard.Mcp.Explorer.Api.Controllers.v1;

/// <summary>
/// Provides Azure contextual data (account info, app registrations, Key Vault inventory)
/// to power the connection form's Azure Assist feature.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public sealed class AzureController(IAzureContextService azureContextService) : ControllerBase
{
    // Azure Key Vault naming rules: 3-24 chars, start/end with alphanumeric, hyphens allowed.
    private static readonly Regex VaultNamePattern = new(@"^[a-zA-Z][a-zA-Z0-9\-]{1,22}[a-zA-Z0-9]$", RegexOptions.Compiled);

    /// <summary>Returns the active Azure account and subscription info.</summary>
    [HttpGet("account")]
    public async Task<IActionResult> GetAccount(CancellationToken cancellationToken)
    {
        var info = await azureContextService.GetAccountInfoAsync(cancellationToken);
        if (info is null)
            return StatusCode(503, new
            {
                error = "Azure credentials unavailable.",
                detail = "No valid Azure credential was found. If running in Docker, set AZURE_CONFIG_DIR=/path/to/.azure in your .env file and rebuild. If running locally, run 'az login'."
            });

        return Ok(info);
    }

    /// <summary>Lists all app registrations visible to the current credential.</summary>
    [HttpGet("app-registrations")]
    public async Task<IActionResult> GetAppRegistrations(CancellationToken cancellationToken)
    {
        var apps = await azureContextService.GetAppRegistrationsAsync(cancellationToken);
        return Ok(apps);
    }

    /// <summary>Lists all subscriptions accessible to the current credential.</summary>
    [HttpGet("subscriptions")]
    public async Task<IActionResult> GetSubscriptions(CancellationToken cancellationToken)
    {
        var subs = await azureContextService.GetSubscriptionsAsync(cancellationToken);
        return Ok(subs);
    }

    /// <summary>Lists all Key Vaults in the specified subscription (or the default if omitted).</summary>
    [HttpGet("keyvaults")]
    public async Task<IActionResult> GetKeyVaults([FromQuery] string? subscriptionId, CancellationToken cancellationToken)
    {
        var vaults = await azureContextService.GetKeyVaultsAsync(subscriptionId, cancellationToken);
        return Ok(vaults);
    }

    /// <summary>Lists all enabled secret names in the specified Key Vault.</summary>
    [HttpGet("keyvaults/{vaultName}/secrets")]
    public async Task<IActionResult> GetKeyVaultSecrets(string vaultName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(vaultName))
            return BadRequest(new { error = "vaultName is required." });

        if (!VaultNamePattern.IsMatch(vaultName))
            return BadRequest(new { error = "vaultName must be 3-24 characters, start and end with an alphanumeric character, and contain only letters, digits, and hyphens." });

        var secrets = await azureContextService.GetKeyVaultSecretNamesAsync(vaultName, cancellationToken);
        return Ok(secrets);
    }
}
