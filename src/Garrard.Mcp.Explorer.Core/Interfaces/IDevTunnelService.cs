using Garrard.Mcp.Explorer.Core.Domain.DevTunnels;

namespace Garrard.Mcp.Explorer.Core.Interfaces;

public interface IDevTunnelService
{
    Task<DevTunnelUserState> GetUserStateAsync(CancellationToken cancellationToken = default);
    IAsyncEnumerable<string> LoginWithDeviceCodeAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DevTunnel>> ListAsync(CancellationToken cancellationToken = default);
    Task<DevTunnel?> GetAsync(string tunnelId, CancellationToken cancellationToken = default);
    Task<DevTunnel> CreateAsync(CreateDevTunnelRequest request, CancellationToken cancellationToken = default);
    Task<DevTunnel?> StartAsync(string tunnelId, CancellationToken cancellationToken = default);
    Task<DevTunnel?> StopAsync(string tunnelId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string tunnelId, CancellationToken cancellationToken = default);
    Task<WebhookEvent> CaptureAsync(WebhookCaptureRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WebhookEvent>> GetEventsAsync(string tunnelId, int? limit = null, CancellationToken cancellationToken = default);
    IAsyncEnumerable<WebhookEvent> StreamEventsAsync(string tunnelId, CancellationToken cancellationToken = default);
    Task<ReplayWebhookResult> ReplayAsync(ReplayWebhookRequest request, CancellationToken cancellationToken = default);
}
