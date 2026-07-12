using Garrard.Mcp.Explorer.Core.Domain.Certificates;
using Garrard.Mcp.Explorer.Core.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Garrard.Mcp.Explorer.Infrastructure.Certificates;

/// <summary>
/// Holds the latest expiry-monitor snapshot for <c>GET /api/v1/certificates/notifications</c>.
/// Registered as a singleton and updated by <see cref="CertificateExpiryMonitor"/>.
/// </summary>
public sealed class CertificateNotificationState
{
    private CertificateNotificationsSnapshot _current = new();

    public CertificateNotificationsSnapshot Current => _current;

    public void Update(CertificateNotificationsSnapshot snapshot) => _current = snapshot;
}

/// <summary>
/// Background pass over the certificate store: an immediate check on startup, then every
/// 12 hours. Flags certificates expiring within 30 days (or expired), and best-effort
/// verifies uploaded certificates against their app registrations to detect stale key
/// credentials. Graph/offline failures are swallowed — this monitor must never break startup.
/// </summary>
public sealed class CertificateExpiryMonitor(
    ICertificateService certificateService,
    ICertificateUploadService uploadService,
    CertificateNotificationState state,
    ILogger<CertificateExpiryMonitor> logger) : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(12);
    private const int ExpiryWarningDays = 30;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // First pass includes the (slower) staleness verification; periodic re-checks too.
        await RunCheckAsync(stoppingToken).ConfigureAwait(false);

        using var timer = new PeriodicTimer(CheckInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                await RunCheckAsync(stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
    }

    private async Task RunCheckAsync(CancellationToken cancellationToken)
    {
        try
        {
            var now = DateTimeOffset.UtcNow;
            var needingAttention = await certificateService.GetExpiringAsync(ExpiryWarningDays, cancellationToken).ConfigureAwait(false);
            var expired = needingAttention.Where(c => c.NotAfter is { } end && end <= now).ToList();
            var expiring = needingAttention.Except(expired).ToList();

            foreach (var cert in expired)
                logger.LogWarning("Certificate {Name} expired on {NotAfter:u}", cert.Name, cert.NotAfter);
            foreach (var cert in expiring)
                logger.LogWarning("Certificate {Name} expires on {NotAfter:u} (within {Days} days)", cert.Name, cert.NotAfter, ExpiryWarningDays);

            var staleUploads = await VerifyUploadsAsync(cancellationToken).ConfigureAwait(false);

            state.Update(new CertificateNotificationsSnapshot
            {
                LastCheckedAt = now,
                Expiring = expiring,
                Expired = expired,
                StaleUploads = staleUploads,
            });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Certificate expiry check failed — will retry on the next interval");
        }
    }

    private async Task<IReadOnlyList<StaleUploadInfo>> VerifyUploadsAsync(CancellationToken cancellationToken)
    {
        var stale = new List<StaleUploadInfo>();
        try
        {
            var certificates = await certificateService.ListAsync(cancellationToken).ConfigureAwait(false);
            foreach (var cert in certificates.Where(c => c.State == CertificateState.Active && c.UploadedTo.Count > 0))
            {
                foreach (var upload in cert.UploadedTo)
                {
                    try
                    {
                        var status = await uploadService.VerifyUploadAsync(cert.Name, upload.AppId, cancellationToken).ConfigureAwait(false);
                        if (status != UploadStatus.Current)
                        {
                            stale.Add(new StaleUploadInfo(cert.Name, upload.AppId, upload.DisplayName, status.ToString()));
                            logger.LogWarning("Certificate {Name} upload to {App} is {Status}", cert.Name, upload.DisplayName, status);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        // Offline / not signed in to Azure — staleness is best-effort only.
                        logger.LogDebug(ex, "Could not verify upload of {Name} to {AppId}", cert.Name, upload.AppId);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Upload staleness verification skipped");
        }

        return stale;
    }
}
