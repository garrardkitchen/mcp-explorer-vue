using System.Threading.Channels;
using Garrard.Mcp.Explorer.Core.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Garrard.Mcp.Explorer.Infrastructure.Mcp;

/// <summary>
/// Background service that asynchronously updates connection timestamps via a Channel-based queue.
/// Non-blocking and thread-safe — UI operations are never delayed by persistence.
/// </summary>
public sealed class ConnectionUpdateService : BackgroundService
{
    private readonly Channel<string> _channel;
    private readonly IUserPreferencesStore _store;
    private readonly ILogger<ConnectionUpdateService> _logger;

    public ConnectionUpdateService(IUserPreferencesStore store, ILogger<ConnectionUpdateService> logger)
    {
        _store = store;
        _logger = logger;
        _channel = Channel.CreateBounded<string>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });
    }

    /// <summary>
    /// Queues a connection name for <c>LastUsedAt</c> timestamp update. Non-blocking.
    /// </summary>
    public bool QueueUpdate(string connectionName)
    {
        var ok = _channel.Writer.TryWrite(connectionName);
        if (!ok)
            _logger.LogWarning("Could not queue timestamp update for '{Name}' — channel full or closed", connectionName);
        return ok;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ConnectionUpdateService started");
        try
        {
            await foreach (var name in _channel.Reader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
            {
                try { await ProcessAsync(name, stoppingToken).ConfigureAwait(false); }
                catch (Exception ex) { _logger.LogError(ex, "Error processing update for '{Name}'", name); }
            }
        }
        catch (OperationCanceledException) { }
        _logger.LogInformation("ConnectionUpdateService stopped");
    }

    private async Task ProcessAsync(string connectionName, CancellationToken ct)
    {
        const int MaxRetries = 3;
        var delay = TimeSpan.FromMilliseconds(100);

        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                var prefs = await _store.LoadAsync(ct).ConfigureAwait(false);
                var idx = prefs.Connections.FindIndex(c =>
                    string.Equals(c.Name, connectionName, StringComparison.OrdinalIgnoreCase));

                if (idx < 0)
                {
                    _logger.LogDebug("Connection '{Name}' not found for timestamp update", connectionName);
                    return;
                }

                var list = prefs.Connections.ToList();
                list[idx] = new Core.Domain.Connections.ConnectionDefinition
                {
                    Name = list[idx].Name,
                    Endpoint = list[idx].Endpoint,
                    AuthenticationMode = list[idx].AuthenticationMode,
                    Headers = list[idx].Headers,
                    AzureCredentials = list[idx].AzureCredentials,
                    OAuthOptions = list[idx].OAuthOptions,
                    Note = list[idx].Note,
                    GroupName = list[idx].GroupName,
                    CreatedAt = list[idx].CreatedAt,
                    LastUpdatedAt = list[idx].LastUpdatedAt,
                    LastUsedAt = DateTime.UtcNow
                };

                await _store.SaveAsync(prefs with { Connections = list }, ct).ConfigureAwait(false);
                _logger.LogDebug("Updated LastUsedAt for '{Name}'", connectionName);
                return;
            }
            catch (Exception ex) when (attempt < MaxRetries)
            {
                _logger.LogWarning(ex, "Retry {A}/{M} for '{Name}'", attempt, MaxRetries, connectionName);
                await Task.Delay(delay, ct).ConfigureAwait(false);
                delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * 2);
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _channel.Writer.Complete();
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }
}

