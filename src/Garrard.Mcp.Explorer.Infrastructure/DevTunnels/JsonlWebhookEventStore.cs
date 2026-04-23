using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Garrard.Mcp.Explorer.Core.Domain.DevTunnels;
using Garrard.Mcp.Explorer.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Garrard.Mcp.Explorer.Infrastructure.DevTunnels;

public sealed class JsonlWebhookEventStore : IWebhookEventStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, List<Channel<WebhookEvent>>> _subscribers = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _baseDirectory;
    private readonly ILogger<JsonlWebhookEventStore> _logger;
    private readonly int _retention;

    public JsonlWebhookEventStore(IConfiguration configuration, ILogger<JsonlWebhookEventStore> logger)
    {
        _logger = logger;
        _retention = Math.Max(1, configuration.GetValue("DevTunnels:Retention", 1000));

        var configuredPath = configuration["DevTunnels:DataPath"]
            ?? Environment.GetEnvironmentVariable("DEVTUNNELS__DataPath");

        _baseDirectory = string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "McpExplorer",
                "DevTunnels")
            : configuredPath;
    }

    public async Task AppendAsync(WebhookEvent webhookEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(webhookEvent);

        var gate = _locks.GetOrAdd(webhookEvent.TunnelId, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(_baseDirectory);
            var filePath = GetFilePath(webhookEvent.TunnelId);
            var json = JsonSerializer.Serialize(webhookEvent, SerializerOptions);
            await File.AppendAllTextAsync(filePath, json + Environment.NewLine, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
            await TrimIfNeededAsync(filePath, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }

        Publish(webhookEvent);
    }

    public async Task<WebhookEvent?> GetAsync(string tunnelId, string eventId, CancellationToken cancellationToken = default)
    {
        var events = await LoadEventsAsync(tunnelId, cancellationToken).ConfigureAwait(false);
        return events.FirstOrDefault(e => string.Equals(e.Id, eventId, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<IReadOnlyList<WebhookEvent>> GetRecentAsync(string tunnelId, int? limit = null, CancellationToken cancellationToken = default)
    {
        var events = await LoadEventsAsync(tunnelId, cancellationToken).ConfigureAwait(false);
        if (limit is > 0)
            events = events.TakeLast(limit.Value).ToList();

        return events
            .OrderByDescending(e => e.ReceivedAtUtc)
            .ToList();
    }

    public async IAsyncEnumerable<WebhookEvent> StreamAsync(
        string tunnelId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var channel = Channel.CreateUnbounded<WebhookEvent>();
        var subscriberList = _subscribers.GetOrAdd(tunnelId, static _ => []);

        lock (subscriberList)
            subscriberList.Add(channel);

        try
        {
            await foreach (var item in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
                yield return item;
        }
        finally
        {
            lock (subscriberList)
                subscriberList.Remove(channel);

            channel.Writer.TryComplete();
        }
    }

    private async Task<List<WebhookEvent>> LoadEventsAsync(string tunnelId, CancellationToken cancellationToken)
    {
        var filePath = GetFilePath(tunnelId);
        if (!File.Exists(filePath))
            return [];

        var lines = await File.ReadAllLinesAsync(filePath, cancellationToken).ConfigureAwait(false);
        var events = new List<WebhookEvent>(lines.Length);

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            try
            {
                var evt = JsonSerializer.Deserialize<WebhookEvent>(line, SerializerOptions);
                if (evt is not null)
                    events.Add(evt);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Could not parse webhook event line for tunnel {TunnelId}", tunnelId);
            }
        }

        return events;
    }

    private async Task TrimIfNeededAsync(string filePath, CancellationToken cancellationToken)
    {
        var lines = await File.ReadAllLinesAsync(filePath, cancellationToken).ConfigureAwait(false);
        if (lines.Length <= _retention)
            return;

        var trimmed = lines.TakeLast(_retention);
        await File.WriteAllLinesAsync(filePath, trimmed, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
    }

    private void Publish(WebhookEvent webhookEvent)
    {
        if (!_subscribers.TryGetValue(webhookEvent.TunnelId, out var subscriberList))
            return;

        List<Channel<WebhookEvent>> channels;
        lock (subscriberList)
            channels = [.. subscriberList];

        foreach (var channel in channels)
            channel.Writer.TryWrite(webhookEvent);
    }

    private string GetFilePath(string tunnelId)
        => Path.Combine(_baseDirectory, $"{SanitizeFileName(tunnelId)}.jsonl");

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.Select(c => invalid.Contains(c) ? '-' : c).ToArray();
        return new string(chars);
    }
}
