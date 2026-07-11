using System.Text.RegularExpressions;
using Garrard.Mcp.Explorer.Core.Domain.Certificates;
using Garrard.Mcp.Explorer.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Garrard.Mcp.Explorer.Infrastructure.Certificates;

/// <summary>
/// Orchestrates certificate rotation across the local store, Microsoft Graph, and the
/// connection/HTTP API stores. Ordered so the old certificate keeps working until the
/// new one is in place: generate → upload everywhere → repoint references → optional
/// old-credential cleanup → mark superseded.
/// </summary>
public sealed partial class CertificateRenewalService(
    ICertificateService certificateService,
    ICertificateUploadService uploadService,
    IUserPreferencesStore preferencesStore,
    IHttpApiStore httpApiStore,
    ILogger<CertificateRenewalService> logger) : ICertificateRenewalService
{
    private static readonly int[] AllowedValidityMonths = [6, 12, 24];

    [GeneratedRegex(@"-r(\d+)$")]
    private static partial Regex RenewSuffixRegex();

    public async Task<OperationResult> RenewAsync(string certificateName, bool removeOldKeyCredential, CancellationToken cancellationToken = default)
    {
        var steps = new List<StepResult>();

        // ── Step 1: load the certificate being renewed ────────────────────────
        CertificateInfo? old;
        try
        {
            old = await certificateService.GetAsync(certificateName, cancellationToken).ConfigureAwait(false)
                  ?? throw new FileNotFoundException($"Certificate '{certificateName}' does not exist.");
            if (old.State == CertificateState.Superseded)
                throw new InvalidOperationException($"Certificate '{certificateName}' has already been renewed (superseded by '{old.RenewedBy}').");
            if (old.State == CertificateState.CsrPending)
                throw new InvalidOperationException($"Certificate '{certificateName}' has a pending CSR and cannot be renewed.");
            steps.Add(new StepResult("load", LoadLabel, StepStatus.Succeeded));
        }
        catch (Exception ex)
        {
            steps.Add(new StepResult("load", LoadLabel, StepStatus.Failed, ex.Message));
            AddSkipped(steps, ["generate", "upload", "repoint", "cleanup", "supersede"]);
            return OperationResult.Failed(steps);
        }

        // ── Step 2: generate the successor ────────────────────────────────────
        var successorName = await NextSuccessorNameAsync(certificateName, cancellationToken).ConfigureAwait(false);
        CertificateInfo successor;
        try
        {
            var generation = await certificateService.GenerateSelfSignedAsync(new GenerateCertificateRequest
            {
                Name = successorName,
                SubjectCn = StripCnPrefix(old.Subject),
                KeySize = old.KeySize > 0 ? old.KeySize : 2048,
                ValidityMonths = DeriveValidityMonths(old),
            }, cancellationToken).ConfigureAwait(false);

            if (!generation.Success || generation.Certificate is null)
            {
                var failure = generation.Steps.FirstOrDefault(s => s.Status == StepStatus.Failed);
                throw new InvalidOperationException(failure?.Message ?? "Certificate generation failed.");
            }

            successor = generation.Certificate;
            steps.Add(new StepResult("generate", GenerateLabel, StepStatus.Succeeded, $"Created '{successorName}'"));
        }
        catch (Exception ex)
        {
            steps.Add(new StepResult("generate", GenerateLabel, StepStatus.Failed, ex.Message));
            AddSkipped(steps, ["upload", "repoint", "cleanup", "supersede"]);
            return OperationResult.Failed(steps);
        }

        // ── Step 3: upload to every app registration the old cert was on ─────
        if (old.UploadedTo.Count == 0)
        {
            steps.Add(new StepResult("upload", UploadLabel, StepStatus.Skipped, "The old certificate was not uploaded anywhere."));
        }
        else
        {
            var failures = new List<string>();
            foreach (var record in old.UploadedTo)
            {
                var upload = await uploadService.UploadAsync(successorName, record.AppId, cancellationToken).ConfigureAwait(false);
                if (!upload.Success)
                {
                    var failure = upload.Steps.FirstOrDefault(s => s.Status == StepStatus.Failed);
                    var appLabel = string.IsNullOrEmpty(record.DisplayName) ? record.AppId : record.DisplayName;
                    failures.Add($"{appLabel}: {failure?.Message ?? "upload failed"}");
                }
            }

            if (failures.Count > 0)
            {
                steps.Add(new StepResult("upload", UploadLabel, StepStatus.Failed,
                    $"{failures.Count} of {old.UploadedTo.Count} upload(s) failed — connections were NOT repointed; " +
                    $"the old certificate is untouched and keeps working. {string.Join(" · ", failures)}"));
                AddSkipped(steps, ["repoint", "cleanup", "supersede"]);
                return OperationResult.Failed(steps, successor);
            }

            steps.Add(new StepResult("upload", UploadLabel, StepStatus.Succeeded,
                $"Uploaded to {old.UploadedTo.Count} app registration(s)"));
        }

        // ── Step 4: repoint references ────────────────────────────────────────
        try
        {
            var (connections, httpApis) = await RepointReferencesAsync(certificateName, successorName, cancellationToken).ConfigureAwait(false);
            steps.Add(new StepResult("repoint", RepointLabel, StepStatus.Succeeded,
                $"{connections} connection(s), {httpApis} HTTP API definition(s)"));
        }
        catch (Exception ex)
        {
            steps.Add(new StepResult("repoint", RepointLabel, StepStatus.Failed, ex.Message));
            AddSkipped(steps, ["cleanup", "supersede"]);
            return OperationResult.Failed(steps, successor);
        }

        // ── Step 5: optionally remove the old key credentials in Azure ───────
        if (!removeOldKeyCredential || old.UploadedTo.Count == 0)
        {
            steps.Add(new StepResult("cleanup", CleanupLabel, StepStatus.Skipped,
                removeOldKeyCredential ? "Nothing to remove." : "Kept — remove it later from the Certificates page."));
        }
        else
        {
            var cleanupFailures = 0;
            foreach (var record in old.UploadedTo)
            {
                try
                {
                    await uploadService.RemoveKeyCredentialAsync(record.AppId, record.KeyId, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    cleanupFailures++;
                    logger.LogWarning(ex, "Could not remove old key credential {KeyId} from {AppId}", record.KeyId, record.AppId);
                }
            }

            steps.Add(cleanupFailures == 0
                ? new StepResult("cleanup", CleanupLabel, StepStatus.Succeeded)
                : new StepResult("cleanup", CleanupLabel, StepStatus.Failed,
                    $"{cleanupFailures} old key credential(s) could not be removed — clean them up from the Certificates page. The renewal itself succeeded."));
        }

        // ── Step 6: mark the old certificate superseded ───────────────────────
        try
        {
            await certificateService.MarkSupersededAsync(certificateName, successorName, cancellationToken).ConfigureAwait(false);
            steps.Add(new StepResult("supersede", SupersedeLabel, StepStatus.Succeeded));
        }
        catch (Exception ex)
        {
            steps.Add(new StepResult("supersede", SupersedeLabel, StepStatus.Failed, ex.Message));
            return OperationResult.Failed(steps, successor);
        }

        logger.LogInformation("Renewed certificate {Old} → {New}", certificateName, successorName);
        var refreshed = await certificateService.GetAsync(successorName, cancellationToken).ConfigureAwait(false);
        return OperationResult.Succeeded(steps, refreshed ?? successor);
    }

    private async Task<(int Connections, int HttpApis)> RepointReferencesAsync(string oldName, string newName, CancellationToken cancellationToken)
    {
        var newRef = new Core.Domain.Connections.CertificateReference { CertificateName = newName };

        var preferences = await preferencesStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        var repointedConnections = 0;
        foreach (var connection in preferences.Connections)
        {
            if (connection.AzureCredentials?.CertificateRef is { } certRef &&
                string.Equals(certRef.CertificateName, oldName, StringComparison.OrdinalIgnoreCase))
            {
                connection.AzureCredentials = connection.AzureCredentials with { CertificateRef = newRef };
                repointedConnections++;
            }
        }
        if (repointedConnections > 0)
            await preferencesStore.SaveAsync(preferences, cancellationToken).ConfigureAwait(false);

        var definitions = await httpApiStore.GetAllDefinitionsAsync(cancellationToken).ConfigureAwait(false);
        var repointedHttpApis = 0;
        foreach (var definition in definitions)
        {
            if (definition.AzureCredentials?.CertificateRef is { } certRef &&
                string.Equals(certRef.CertificateName, oldName, StringComparison.OrdinalIgnoreCase))
            {
                definition.AzureCredentials.CertificateRef = newRef;
                await httpApiStore.SaveDefinitionAsync(definition, cancellationToken).ConfigureAwait(false);
                repointedHttpApis++;
            }
        }

        return (repointedConnections, repointedHttpApis);
    }

    private async Task<string> NextSuccessorNameAsync(string name, CancellationToken cancellationToken)
    {
        var match = RenewSuffixRegex().Match(name);
        var baseName = match.Success ? name[..match.Index] : name;
        var next = match.Success ? int.Parse(match.Groups[1].Value) + 1 : 2;

        while (true)
        {
            var candidate = $"{baseName}-r{next}";
            if (await certificateService.GetAsync(candidate, cancellationToken).ConfigureAwait(false) is null)
                return candidate;
            next++;
        }
    }

    private static string StripCnPrefix(string subject)
    {
        var value = subject.Trim();
        return value.StartsWith("CN=", StringComparison.OrdinalIgnoreCase) ? value[3..] : value;
    }

    /// <summary>Re-uses the old certificate's validity window, snapped to the allowed options.</summary>
    private static int DeriveValidityMonths(CertificateInfo old)
    {
        if (old.NotBefore is not { } notBefore || old.NotAfter is not { } notAfter)
            return 12;

        var approxMonths = (notAfter - notBefore).TotalDays / 30.4;
        return AllowedValidityMonths.OrderBy(m => Math.Abs(m - approxMonths)).First();
    }

    private const string LoadLabel = "Load the certificate being renewed";
    private const string GenerateLabel = "Generate the successor certificate";
    private const string UploadLabel = "Upload the successor to app registrations";
    private const string RepointLabel = "Repoint referencing connections";
    private const string CleanupLabel = "Remove the old key credentials from Azure";
    private const string SupersedeLabel = "Mark the old certificate superseded";

    private static readonly Dictionary<string, string> StepLabels = new()
    {
        ["generate"] = GenerateLabel,
        ["upload"] = UploadLabel,
        ["repoint"] = RepointLabel,
        ["cleanup"] = CleanupLabel,
        ["supersede"] = SupersedeLabel,
    };

    private static void AddSkipped(List<StepResult> steps, string[] remaining)
    {
        foreach (var id in remaining)
            steps.Add(new StepResult(id, StepLabels.GetValueOrDefault(id, id), StepStatus.Skipped));
    }
}
