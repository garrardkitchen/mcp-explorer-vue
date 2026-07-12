using System.Security.Cryptography.X509Certificates;
using Azure.Core;
using Azure.Identity;
using Garrard.Mcp.Explorer.Core.Domain.Certificates;
using Garrard.Mcp.Explorer.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using OperationResult = Garrard.Mcp.Explorer.Core.Domain.Certificates.OperationResult;

namespace Garrard.Mcp.Explorer.Infrastructure.Certificates;

/// <summary>
/// Uploads certificate public keys to Azure App Registrations via Microsoft Graph and
/// inspects/removes keyCredentials. Uses the same <c>DefaultAzureCredential</c> chain as
/// <see cref="Azure.AzureContextService"/>. Graph PATCHes replace the whole keyCredentials
/// list, so all mutations are serialized through a single semaphore.
/// </summary>
public sealed class GraphKeyCredentialService : ICertificateUploadService
{
    private readonly ICertificateService _certificateService;
    private readonly ILogger<GraphKeyCredentialService> _logger;
    private readonly DefaultAzureCredential _credential;
    private readonly SemaphoreSlim _patchLock = new(1, 1);

    public GraphKeyCredentialService(ICertificateService certificateService, ILogger<GraphKeyCredentialService> logger)
    {
        _certificateService = certificateService;
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

    private GraphServiceClient CreateGraphClient()
        => new(_credential, ["https://graph.microsoft.com/.default"]);

    public async Task<OperationResult> UploadAsync(string certificateName, string appId, CancellationToken cancellationToken = default)
    {
        var steps = new List<StepResult>();

        // Step 1 — load the local certificate (public part only)
        X509Certificate2? certificate = null;
        CertificateInfo? info = null;
        try
        {
            ValidateAppId(appId);
            info = await _certificateService.GetAsync(certificateName, cancellationToken).ConfigureAwait(false)
                   ?? throw new FileNotFoundException($"Certificate '{certificateName}' does not exist.");
            if (info.State == CertificateState.CsrPending)
                throw new InvalidOperationException($"Certificate '{certificateName}' has a pending CSR — import the issued certificate first.");

            var pem = await _certificateService.GetPublicCertPemAsync(certificateName, cancellationToken).ConfigureAwait(false);
            certificate = X509Certificate2.CreateFromPem(pem);
            steps.Add(new StepResult("load-cert", "Load certificate from the local store", StepStatus.Succeeded));
        }
        catch (Exception ex)
        {
            steps.Add(new StepResult("load-cert", "Load certificate from the local store", StepStatus.Failed, ex.Message));
            AddSkipped(steps, [("fetch-app", FetchAppLabel), ("add-key", AddKeyLabel), ("verify", VerifyLabel)]);
            return OperationResult.Failed(steps);
        }

        using var _ = certificate;
        var graphClient = CreateGraphClient();

        // Step 2 — fetch the app registration (also proves we can reach Graph)
        Application? application;
        try
        {
            application = await GetApplicationByAppIdAsync(graphClient, appId, cancellationToken).ConfigureAwait(false)
                          ?? throw new InvalidOperationException($"No app registration found with client id '{appId}'.");
            steps.Add(new StepResult("fetch-app", FetchAppLabel, StepStatus.Succeeded,
                $"Found '{application.DisplayName}'"));
        }
        catch (Exception ex)
        {
            steps.Add(new StepResult("fetch-app", FetchAppLabel, StepStatus.Failed, DescribeGraphError(ex)));
            AddSkipped(steps, [("add-key", AddKeyLabel), ("verify", VerifyLabel)]);
            return OperationResult.Failed(steps);
        }

        // Step 3 — append the key credential (idempotent by thumbprint)
        var thumbprintBytes = Convert.FromHexString(certificate.Thumbprint);
        var preExistingKeyIds = new HashSet<Guid>();
        try
        {
            var existing = FindByCustomKeyIdentifier(application.KeyCredentials, thumbprintBytes);
            if (existing is not null)
            {
                steps.Add(new StepResult("add-key", AddKeyLabel, StepStatus.Skipped,
                    "A key credential with this thumbprint already exists — nothing to upload."));
            }
            else
            {
                await _patchLock.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    // Re-read inside the lock: PATCH replaces the whole list, so we must
                    // append to the freshest snapshot to avoid dropping concurrent additions.
                    var fresh = await GetApplicationByAppIdAsync(graphClient, appId, cancellationToken).ConfigureAwait(false)
                                ?? throw new InvalidOperationException($"App registration '{appId}' disappeared during upload.");

                    // Snapshot the key ids that existed before our PATCH so the verify step
                    // can identify the credential we added even before customKeyIdentifier
                    // becomes visible on read replicas.
                    foreach (var k in fresh.KeyCredentials ?? [])
                        if (k.KeyId is { } id) preExistingKeyIds.Add(id);

                    var updated = new List<KeyCredential>(fresh.KeyCredentials ?? [])
                    {
                        new()
                        {
                            Type = "AsymmetricX509Cert",
                            Usage = "Verify",
                            Key = certificate.RawData,
                            DisplayName = $"mcp-explorer:{certificateName}",
                            StartDateTime = certificate.NotBefore.ToUniversalTime(),
                            EndDateTime = certificate.NotAfter.ToUniversalTime(),
                        }
                    };

                    await graphClient.Applications[fresh.Id]
                        .PatchAsync(new Application { KeyCredentials = updated }, cancellationToken: cancellationToken)
                        .ConfigureAwait(false);
                }
                finally
                {
                    _patchLock.Release();
                }

                steps.Add(new StepResult("add-key", AddKeyLabel, StepStatus.Succeeded));
            }
        }
        catch (Exception ex)
        {
            steps.Add(new StepResult("add-key", AddKeyLabel, StepStatus.Failed, DescribeGraphError(ex)));
            AddSkipped(steps, [("verify", VerifyLabel)]);
            return OperationResult.Failed(steps);
        }

        // Step 4 — verify the thumbprint landed and record the upload locally.
        // Graph reads can lag the PATCH (eventual consistency), so poll with backoff
        // before concluding anything.
        try
        {
            var expectedDisplayName = $"mcp-explorer:{certificateName}";
            Application? verified = null;
            KeyCredential? credential = null;
            string? matchNote = null;

            foreach (var delaySeconds in VerifyRetryDelaysSeconds)
            {
                if (delaySeconds > 0)
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken).ConfigureAwait(false);

                verified = await GetApplicationByAppIdAsync(graphClient, appId, cancellationToken).ConfigureAwait(false);
                credential = FindByCustomKeyIdentifier(verified?.KeyCredentials, thumbprintBytes);
                if (credential is not null) break;

                // Fallback: the customKeyIdentifier can lag behind the credential itself —
                // identify the one we just added by key-id diff + our display name.
                credential = (verified?.KeyCredentials ?? [])
                    .FirstOrDefault(k => k.KeyId is { } id && !preExistingKeyIds.Contains(id) &&
                                         string.Equals(k.DisplayName, expectedDisplayName, StringComparison.Ordinal));
                if (credential is not null)
                {
                    matchNote = " (matched by key id; customKeyIdentifier not visible yet)";
                    break;
                }
            }

            await _certificateService.RecordUploadAsync(certificateName, new CertificateUploadRecord
            {
                AppObjectId = verified?.Id ?? application.Id ?? string.Empty,
                AppId = appId,
                DisplayName = verified?.DisplayName ?? application.DisplayName ?? string.Empty,
                KeyId = credential?.KeyId?.ToString() ?? string.Empty,
                UploadedThumbprintSha1 = certificate.Thumbprint,
                UploadedAt = DateTimeOffset.UtcNow,
            }, cancellationToken).ConfigureAwait(false);

            if (credential is null)
            {
                // The PATCH itself succeeded, so this is propagation delay — not a failure.
                // The upload is recorded locally; a later Verify reconciles the key id.
                steps.Add(new StepResult("verify", VerifyLabel, StepStatus.Skipped,
                    "Upload succeeded, but the key credential was not visible on re-read yet (Entra ID propagation). " +
                    "Use 'Verify' on the Certificates page in a minute to confirm."));
            }
            else
            {
                steps.Add(new StepResult("verify", VerifyLabel, StepStatus.Succeeded,
                    $"customKeyIdentifier matches {certificate.Thumbprint[..8]}…{matchNote}"));
            }

            var latest = await _certificateService.GetAsync(certificateName, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Uploaded certificate {Name} to app registration {AppId} (verified: {Verified})",
                certificateName, appId, credential is not null);
            return OperationResult.Succeeded(steps, latest ?? info);
        }
        catch (Exception ex)
        {
            steps.Add(new StepResult("verify", VerifyLabel, StepStatus.Failed, DescribeGraphError(ex)));
            return OperationResult.Failed(steps, info);
        }
    }

    // First attempt immediately, then back off — ~15s total before treating the
    // missing credential as propagation delay.
    private static readonly int[] VerifyRetryDelaysSeconds = [0, 2, 3, 4, 6];

    public async Task<IReadOnlyList<GraphKeyCredentialInfo>> ListKeyCredentialsAsync(string appId, CancellationToken cancellationToken = default)
    {
        ValidateAppId(appId);
        var graphClient = CreateGraphClient();
        var application = await GetApplicationByAppIdAsync(graphClient, appId, cancellationToken).ConfigureAwait(false)
                          ?? throw new InvalidOperationException($"No app registration found with client id '{appId}'.");

        var localCerts = await _certificateService.ListAsync(cancellationToken).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow;

        return (application.KeyCredentials ?? [])
            .Select(k =>
            {
                var thumbHex = k.CustomKeyIdentifier is { Length: > 0 } id ? Convert.ToHexString(id) : null;
                var local = thumbHex is null
                    ? null
                    : localCerts.FirstOrDefault(c => string.Equals(c.ThumbprintSha1, thumbHex, StringComparison.OrdinalIgnoreCase));
                var expired = k.EndDateTime is { } end && end < now;
                var superseded = local?.State == CertificateState.Superseded;

                // Superseded wins the wording — it's the actionable reason even if the
                // credential also happens to be past its end date.
                var staleReason = superseded
                    ? $"superseded by {local!.RenewedBy ?? "a newer certificate"}"
                    : expired
                        ? $"expired {k.EndDateTime:yyyy-MM-dd}"
                        : null;

                return new GraphKeyCredentialInfo(
                    KeyId: k.KeyId?.ToString() ?? string.Empty,
                    DisplayName: k.DisplayName,
                    CustomKeyIdentifierHex: thumbHex,
                    StartDateTime: k.StartDateTime,
                    EndDateTime: k.EndDateTime,
                    LocalCertificateName: local?.Name,
                    IsStale: staleReason is not null,
                    StaleReason: staleReason);
            })
            .ToList();
    }

    public async Task RemoveKeyCredentialAsync(string appId, string keyId, CancellationToken cancellationToken = default)
    {
        ValidateAppId(appId);
        if (!Guid.TryParse(keyId, out var keyGuid))
            throw new ArgumentException("keyId must be a GUID.", nameof(keyId));

        var graphClient = CreateGraphClient();

        await _patchLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var application = await GetApplicationByAppIdAsync(graphClient, appId, cancellationToken).ConfigureAwait(false)
                              ?? throw new InvalidOperationException($"No app registration found with client id '{appId}'.");

            var remaining = (application.KeyCredentials ?? []).Where(k => k.KeyId != keyGuid).ToList();
            if (remaining.Count == (application.KeyCredentials?.Count ?? 0))
                return; // nothing to remove

            await graphClient.Applications[application.Id]
                .PatchAsync(new Application { KeyCredentials = remaining }, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _patchLock.Release();
        }

        // Best-effort: clear matching local upload records so the UI stops showing the app.
        var localCerts = await _certificateService.ListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var cert in localCerts.Where(c => c.UploadedTo.Any(u => string.Equals(u.KeyId, keyId, StringComparison.OrdinalIgnoreCase))))
        {
            await _certificateService.RemoveUploadRecordAsync(cert.Name, keyId, cancellationToken).ConfigureAwait(false);
        }

        _logger.LogInformation("Removed key credential {KeyId} from app registration {AppId}", keyId, appId);
    }

    public async Task<UploadStatus> VerifyUploadAsync(string certificateName, string appId, CancellationToken cancellationToken = default)
    {
        ValidateAppId(appId);
        var info = await _certificateService.GetAsync(certificateName, cancellationToken).ConfigureAwait(false)
                   ?? throw new FileNotFoundException($"Certificate '{certificateName}' does not exist.");
        if (string.IsNullOrEmpty(info.ThumbprintSha1))
            return UploadStatus.Missing;

        var graphClient = CreateGraphClient();
        var application = await GetApplicationByAppIdAsync(graphClient, appId, cancellationToken).ConfigureAwait(false)
                          ?? throw new InvalidOperationException($"No app registration found with client id '{appId}'.");

        var credentials = application.KeyCredentials ?? [];
        var thumbprintBytes = Convert.FromHexString(info.ThumbprintSha1);

        var byThumbprint = FindByCustomKeyIdentifier(credentials, thumbprintBytes);
        if (byThumbprint is not null)
        {
            // Self-heal: uploads verified during an Entra ID propagation window are recorded
            // without a key id — fill it in now that the credential is visible.
            var record = info.UploadedTo.FirstOrDefault(u => string.Equals(u.AppId, appId, StringComparison.OrdinalIgnoreCase));
            if (record is not null && string.IsNullOrEmpty(record.KeyId) && byThumbprint.KeyId is { } keyId)
            {
                await _certificateService.RecordUploadAsync(certificateName, record with { KeyId = keyId.ToString() }, cancellationToken)
                    .ConfigureAwait(false);
            }

            var expired = byThumbprint.EndDateTime is { } end && end < DateTimeOffset.UtcNow;
            return expired ? UploadStatus.Stale : UploadStatus.Current;
        }

        // The thumbprint is gone; if our recorded keyId still exists it points at an old cert → stale.
        var recordedKeyIds = info.UploadedTo
            .Where(u => string.Equals(u.AppId, appId, StringComparison.OrdinalIgnoreCase))
            .Select(u => u.KeyId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return credentials.Any(k => k.KeyId is { } id && recordedKeyIds.Contains(id.ToString()))
            ? UploadStatus.Stale
            : UploadStatus.Missing;
    }

    public async Task<OperationResult> TestTokenAsync(string certificateName, string tenantId, string clientId, string scope, CancellationToken cancellationToken = default)
    {
        var steps = new List<StepResult>();

        X509Certificate2? certificate = null;
        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
            ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
            ArgumentException.ThrowIfNullOrWhiteSpace(scope);
            certificate = await _certificateService.LoadWithPrivateKeyAsync(certificateName, cancellationToken).ConfigureAwait(false);
            steps.Add(new StepResult("load-cert", "Load certificate and private key", StepStatus.Succeeded));
        }
        catch (Exception ex)
        {
            steps.Add(new StepResult("load-cert", "Load certificate and private key", StepStatus.Failed, ex.Message));
            AddSkipped(steps, [("acquire-token", AcquireTokenLabel)]);
            return OperationResult.Failed(steps);
        }

        using var _ = certificate;
        try
        {
            var credential = new ClientCertificateCredential(tenantId.Trim(), clientId.Trim(), certificate,
                new ClientCertificateCredentialOptions { SendCertificateChain = true });
            var token = await credential.GetTokenAsync(new TokenRequestContext([scope.Trim()]), cancellationToken).ConfigureAwait(false);

            steps.Add(new StepResult("acquire-token", AcquireTokenLabel, StepStatus.Succeeded,
                $"Token acquired; expires {token.ExpiresOn:HH:mm:ss} UTC"));
            return OperationResult.Succeeded(steps);
        }
        catch (Exception ex)
        {
            steps.Add(new StepResult("acquire-token", AcquireTokenLabel, StepStatus.Failed,
                $"{ex.Message} Newly uploaded certificates can take 30-60 seconds to propagate in Entra ID — retry shortly."));
            return OperationResult.Failed(steps);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private const string FetchAppLabel = "Fetch the app registration via Microsoft Graph";
    private const string AddKeyLabel = "Add the certificate to keyCredentials";
    private const string VerifyLabel = "Verify the thumbprint on the app registration";
    private const string AcquireTokenLabel = "Acquire a token with ClientCertificateCredential";

    private static void ValidateAppId(string appId)
    {
        if (!Guid.TryParse(appId, out _))
            throw new ArgumentException("appId must be a GUID (the application's client id).", nameof(appId));
    }

    private static async Task<Application?> GetApplicationByAppIdAsync(GraphServiceClient graphClient, string appId, CancellationToken cancellationToken)
    {
        var apps = await graphClient.Applications.GetAsync(req =>
        {
            req.QueryParameters.Filter = $"appId eq '{appId}'";
            req.QueryParameters.Select = ["id", "appId", "displayName", "keyCredentials"];
            req.QueryParameters.Top = 1;
        }, cancellationToken).ConfigureAwait(false);

        return apps?.Value?.FirstOrDefault();
    }

    private static KeyCredential? FindByCustomKeyIdentifier(IEnumerable<KeyCredential>? credentials, byte[] thumbprintBytes)
        => credentials?.FirstOrDefault(k => k.CustomKeyIdentifier is { } id && id.AsSpan().SequenceEqual(thumbprintBytes));

    private static void AddSkipped(List<StepResult> steps, (string Id, string Label)[] remaining)
    {
        foreach (var (id, label) in remaining)
            steps.Add(new StepResult(id, label, StepStatus.Skipped));
    }

    private static string DescribeGraphError(Exception ex)
    {
        if (ex is Microsoft.Graph.Models.ODataErrors.ODataError odata)
        {
            var code = odata.Error?.Code;
            var message = odata.Error?.Message ?? odata.Message;
            if (string.Equals(code, "Authorization_RequestDenied", StringComparison.OrdinalIgnoreCase) ||
                odata.ResponseStatusCode == 403)
            {
                return "Insufficient Microsoft Graph permissions. Your account needs Application.ReadWrite.OwnedBy " +
                       "(or Owner on the app registration). Grant it, then retry — the certificate is safe in the local store.";
            }
            return string.IsNullOrWhiteSpace(code) ? message ?? ex.Message : $"{code}: {message}";
        }

        return ex.Message;
    }
}
