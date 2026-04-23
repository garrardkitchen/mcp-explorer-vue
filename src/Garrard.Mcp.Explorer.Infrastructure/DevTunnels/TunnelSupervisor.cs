using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Garrard.Mcp.Explorer.Infrastructure.DevTunnels;

public sealed class TunnelSupervisor : IHostedService
{
    private readonly DevTunnelService _devTunnelService;
    private readonly ILogger<TunnelSupervisor> _logger;

    public TunnelSupervisor(DevTunnelService devTunnelService, ILogger<TunnelSupervisor> logger)
    {
        _devTunnelService = devTunnelService;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("TunnelSupervisor starting");
        await _devTunnelService.RestartPersistedRunningTunnelsAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("TunnelSupervisor stopping");
        await _devTunnelService.StopAllAsync(cancellationToken).ConfigureAwait(false);

        var tunnels = await _devTunnelService.ListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var tunnel in tunnels.Where(t => t.DeleteOnExit))
        {
            try
            {
                await _devTunnelService.DeleteAsync(tunnel.Id, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not delete dev tunnel {TunnelId} during shutdown", tunnel.Id);
            }
        }
    }
}
