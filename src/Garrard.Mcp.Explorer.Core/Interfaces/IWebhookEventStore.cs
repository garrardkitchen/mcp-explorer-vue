using Garrard.Mcp.Explorer.Core.Domain.DevTunnels;

namespace Garrard.Mcp.Explorer.Core.Interfaces;

public interface IWebhookEventStore
{
    Task AppendAsync(WebhookEvent webhookEvent, CancellationToken cancellationToken = default);
    Task<WebhookEvent?> GetAsync(string tunnelId, string eventId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WebhookEvent>> GetRecentAsync(string tunnelId, int? limit = null, CancellationToken cancellationToken = default);
    IAsyncEnumerable<WebhookEvent> StreamAsync(string tunnelId, CancellationToken cancellationToken = default);
}
