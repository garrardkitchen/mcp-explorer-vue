using Garrard.Mcp.Explorer.Core.Domain.DevTunnels;

namespace Garrard.Mcp.Explorer.Core.Interfaces;

public interface IDevTunnelCli
{
    Task<DevTunnelUserState> GetUserStateAsync(CancellationToken cancellationToken = default);
    IAsyncEnumerable<string> LoginWithDeviceCodeAsync(CancellationToken cancellationToken = default);
    /// <summary>
    /// Creates the tunnel (if missing) and port registration, then returns the service-assigned
    /// tunnel ID (e.g. "relaxed-plant-abc123" without the cluster suffix). This ID must be used
    /// for all subsequent <see cref="HostAsync"/> / <see cref="DeleteTunnelAsync"/> calls.
    /// If <paramref name="existingCliTunnelId"/> is supplied, it is reused and no create call is made.
    /// </summary>
    Task<string> EnsureTunnelExistsAsync(
        string tunnelName,
        TunnelAccess access,
        int port,
        string? existingCliTunnelId = null,
        CancellationToken cancellationToken = default);
    IAsyncEnumerable<DevTunnelHostUpdate> HostAsync(
        string tunnelId,
        TunnelAccess access,
        int port,
        CancellationToken cancellationToken = default);
    Task DeleteTunnelAsync(string tunnelId, TunnelAccess access, CancellationToken cancellationToken = default);
}
