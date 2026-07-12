using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using Azure.Identity;
using Azure.Security.KeyVault.Certificates;
using Garrard.Mcp.Explorer.Core.Domain.Certificates;
using Garrard.Mcp.Explorer.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Garrard.Mcp.Explorer.Infrastructure.Certificates;

/// <summary>
/// Imports certificates from / exports certificates to Azure Key Vault using the same
/// <c>DefaultAzureCredential</c> chain as the Key Vault secret resolver.
/// </summary>
public sealed class KeyVaultCertificateService : IKeyVaultCertificateService
{
    // Same vault-name allow-list as KeyVaultSecretResolver / AzureController.
    private static readonly Regex VaultNamePattern = new(@"^[a-zA-Z][a-zA-Z0-9\-]{1,22}[a-zA-Z0-9]$", RegexOptions.Compiled);

    private readonly ICertificateService _certificateService;
    private readonly ILogger<KeyVaultCertificateService> _logger;
    private readonly CertificateAuditLog _audit;
    private readonly DefaultAzureCredential _credential;

    public KeyVaultCertificateService(ICertificateService certificateService, ILogger<KeyVaultCertificateService> logger)
    {
        _certificateService = certificateService;
        _logger = logger;
        _audit = new CertificateAuditLog(certificateService.CertificatesDirectory);
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

    private CertificateClient CreateClient(string vaultName)
    {
        if (!VaultNamePattern.IsMatch(vaultName))
            throw new ArgumentException(
                $"Vault name '{vaultName}' does not conform to Azure Key Vault naming rules (3-24 chars, alphanumeric/hyphens).",
                nameof(vaultName));

        return new CertificateClient(new Uri($"https://{vaultName}.vault.azure.net/"), _credential);
    }

    public async Task<IReadOnlyList<string>> ListCertificatesAsync(string vaultName, CancellationToken cancellationToken = default)
    {
        var client = CreateClient(vaultName);
        var names = new List<string>();
        await foreach (var properties in client.GetPropertiesOfCertificatesAsync(cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            if (properties.Enabled != false)
                names.Add(properties.Name);
        }

        names.Sort(StringComparer.OrdinalIgnoreCase);
        return names;
    }

    public async Task<OperationResult> ImportFromKeyVaultAsync(string vaultName, string kvCertificateName, string localName, CancellationToken cancellationToken = default)
    {
        var steps = new List<StepResult>();

        X509Certificate2? certificate = null;
        try
        {
            var client = CreateClient(vaultName);
            var response = await client.DownloadCertificateAsync(kvCertificateName, cancellationToken: cancellationToken).ConfigureAwait(false);
            certificate = response.Value;
            if (!certificate.HasPrivateKey)
                throw new InvalidOperationException("The Key Vault certificate has no exportable private key. Set its policy to exportable, or import a different certificate.");
            steps.Add(new StepResult("download", DownloadLabel, StepStatus.Succeeded, $"Downloaded '{kvCertificateName}' from {vaultName}"));
        }
        catch (Exception ex)
        {
            certificate?.Dispose();
            steps.Add(new StepResult("download", DownloadLabel, StepStatus.Failed, ex.Message));
            steps.Add(new StepResult("store", StoreLabel, StepStatus.Skipped));
            return OperationResult.Failed(steps);
        }

        using var _ = certificate;
        try
        {
            using var rsa = certificate.GetRSAPrivateKey()
                            ?? throw new InvalidOperationException("Only RSA certificates are supported for Azure client-credential auth.");

            var certPem = PemEncoding.WriteString("CERTIFICATE", certificate.RawData);
            var keyPem = PemEncoding.WriteString("PRIVATE KEY", rsa.ExportPkcs8PrivateKey());

            var info = await _certificateService.ImportAsync(localName, certPem, keyPem, null, CertificateSource.KeyVault, cancellationToken).ConfigureAwait(false);
            steps.Add(new StepResult("store", StoreLabel, StepStatus.Succeeded));

            await _audit.AppendAsync("import-keyvault", localName, $"vault={vaultName}; kvName={kvCertificateName}", cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Imported Key Vault certificate {Vault}/{KvName} as {Local}", vaultName, kvCertificateName, localName);
            return OperationResult.Succeeded(steps, info);
        }
        catch (Exception ex)
        {
            steps.Add(new StepResult("store", StoreLabel, StepStatus.Failed, ex.Message));
            return OperationResult.Failed(steps);
        }
    }

    public async Task<OperationResult> ExportToKeyVaultAsync(string localName, string vaultName, CancellationToken cancellationToken = default)
    {
        var steps = new List<StepResult>();

        byte[] pfxBytes;
        try
        {
            using var certificate = await _certificateService.LoadWithPrivateKeyAsync(localName, cancellationToken).ConfigureAwait(false);
            pfxBytes = certificate.Export(X509ContentType.Pfx);
            steps.Add(new StepResult("load", "Load certificate and private key from the local store", StepStatus.Succeeded));
        }
        catch (Exception ex)
        {
            steps.Add(new StepResult("load", "Load certificate and private key from the local store", StepStatus.Failed, ex.Message));
            steps.Add(new StepResult("upload", UploadLabel, StepStatus.Skipped));
            return OperationResult.Failed(steps);
        }

        try
        {
            var client = CreateClient(vaultName);
            await client.ImportCertificateAsync(new ImportCertificateOptions(localName, pfxBytes), cancellationToken).ConfigureAwait(false);
            steps.Add(new StepResult("upload", UploadLabel, StepStatus.Succeeded, $"Stored as '{localName}' in {vaultName}"));

            await _audit.AppendAsync("export-keyvault", localName, $"vault={vaultName}", cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Exported certificate {Local} to Key Vault {Vault}", localName, vaultName);

            var info = await _certificateService.GetAsync(localName, cancellationToken).ConfigureAwait(false);
            return OperationResult.Succeeded(steps, info);
        }
        catch (Exception ex)
        {
            steps.Add(new StepResult("upload", UploadLabel, StepStatus.Failed, ex.Message));
            return OperationResult.Failed(steps);
        }
        finally
        {
            Array.Clear(pfxBytes);
        }
    }

    private const string DownloadLabel = "Download certificate from Key Vault";
    private const string StoreLabel = "Store certificate in the local store";
    private const string UploadLabel = "Import certificate into Key Vault";
}
