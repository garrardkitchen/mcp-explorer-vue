using System.Text;
using System.Text.Json;
using Garrard.Mcp.Explorer.Core.Domain.HttpApi;
using Garrard.Mcp.Explorer.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Garrard.Mcp.Explorer.Infrastructure.HttpApi;

/// <summary>
/// Stores HTTP response snapshots (bookmarks) and lightweight invocation history
/// as JSONL files under HttpApis/{endpointId}/.
/// Never writes to settings.json.
/// </summary>
public sealed class JsonlHttpApiSnapshotStore : IHttpApiSnapshotStore
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);
    private readonly string _baseDir;
    private readonly int _historyRetention;
    private readonly ILogger<JsonlHttpApiSnapshotStore> _logger;

    public JsonlHttpApiSnapshotStore(IConfiguration configuration, ILogger<JsonlHttpApiSnapshotStore> logger)
    {
        _logger = logger;
        _historyRetention = Math.Max(1, configuration.GetValue("HttpApis:HistoryRetention", 500));

        var customPath = configuration["PREFERENCES:StoragePath"]
                         ?? Environment.GetEnvironmentVariable("PREFERENCES__StoragePath");

        var root = string.IsNullOrWhiteSpace(customPath)
            ? Path.Combine(
                OperatingSystem.IsWindows()
                    ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
                    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share"),
                "McpExplorer")
            : Path.GetDirectoryName(customPath)!;

        _baseDir = Path.Combine(root, "HttpApis");
    }

    // ── Snapshots ─────────────────────────────────────────────────────────────

    public async Task AppendSnapshotAsync(HttpResponseSnapshot snapshot, CancellationToken ct = default)
    {
        var path = GetSnapshotPath(snapshot.EndpointId);
        EnsureDir(snapshot.EndpointId);
        var line = JsonSerializer.Serialize(snapshot, _json);
        await File.AppendAllTextAsync(path, line + Environment.NewLine, Encoding.UTF8, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<HttpResponseSnapshot>> GetSnapshotsAsync(string endpointId, CancellationToken ct = default)
    {
        var path = GetSnapshotPath(endpointId);
        return await ReadJsonlAsync<HttpResponseSnapshot>(path, ct).ConfigureAwait(false);
    }

    public async Task<HttpResponseSnapshot?> GetSnapshotAsync(string endpointId, string snapshotId, CancellationToken ct = default)
    {
        var all = await GetSnapshotsAsync(endpointId, ct).ConfigureAwait(false);
        return all.FirstOrDefault(s => string.Equals(s.Id, snapshotId, StringComparison.OrdinalIgnoreCase));
    }

    public async Task DeleteSnapshotAsync(string endpointId, string snapshotId, CancellationToken ct = default)
    {
        var path = GetSnapshotPath(endpointId);
        var all = await ReadJsonlAsync<HttpResponseSnapshot>(path, ct).ConfigureAwait(false);
        var remaining = all.Where(s => !string.Equals(s.Id, snapshotId, StringComparison.OrdinalIgnoreCase)).ToList();
        await WriteJsonlAsync(path, remaining, ct).ConfigureAwait(false);
    }

    // ── Invocation history ────────────────────────────────────────────────────

    public async Task AppendInvocationAsync(HttpApiInvocationRecord record, CancellationToken ct = default)
    {
        var path = GetHistoryPath(record.EndpointId);
        EnsureDir(record.EndpointId);
        var line = JsonSerializer.Serialize(record, _json);
        await File.AppendAllTextAsync(path, line + Environment.NewLine, Encoding.UTF8, ct).ConfigureAwait(false);
        await TrimIfNeededAsync<HttpApiInvocationRecord>(path, _historyRetention, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<HttpApiInvocationRecord>> GetHistoryAsync(
        string endpointId, int? limit = null, CancellationToken ct = default)
    {
        var path = GetHistoryPath(endpointId);
        var all = await ReadJsonlAsync<HttpApiInvocationRecord>(path, ct).ConfigureAwait(false);
        var ordered = all.OrderByDescending(r => r.InvokedAt).ToList();
        return limit.HasValue ? ordered.Take(limit.Value).ToList() : ordered;
    }

    public async Task<IReadOnlyList<HttpApiInvocationRecord>> GetGlobalHistoryAsync(
        int? limit = null, CancellationToken ct = default)
    {
        if (!Directory.Exists(_baseDir)) return [];

        var allRecords = new List<HttpApiInvocationRecord>();
        var perFileLimit = limit.GetValueOrDefault();
        foreach (var dir in Directory.EnumerateDirectories(_baseDir))
        {
            var histPath = Path.Combine(dir, "history.jsonl");
            if (!File.Exists(histPath)) continue;
            var records = limit.HasValue
                ? await ReadJsonlTailAsync<HttpApiInvocationRecord>(histPath, perFileLimit, ct).ConfigureAwait(false)
                : await ReadJsonlAsync<HttpApiInvocationRecord>(histPath, ct).ConfigureAwait(false);
            allRecords.AddRange(records);
        }

        var ordered = allRecords.OrderByDescending(r => r.InvokedAt).ToList();
        return limit.HasValue ? ordered.Take(limit.Value).ToList() : ordered;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private string GetEndpointDir(string endpointId)
        => Path.Combine(_baseDir, Sanitize(endpointId));

    private string GetSnapshotPath(string endpointId)
        => Path.Combine(GetEndpointDir(endpointId), "snapshots.jsonl");

    private string GetHistoryPath(string endpointId)
        => Path.Combine(GetEndpointDir(endpointId), "history.jsonl");

    private void EnsureDir(string endpointId)
        => Directory.CreateDirectory(GetEndpointDir(endpointId));

    private async Task<List<T>> ReadJsonlAsync<T>(string path, CancellationToken ct)
    {
        if (!File.Exists(path)) return [];
        var lines = await File.ReadAllLinesAsync(path, ct).ConfigureAwait(false);
        var result = new List<T>(lines.Length);
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                var item = JsonSerializer.Deserialize<T>(line, _json);
                if (item is not null) result.Add(item);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Skipping malformed JSONL line in {Path}", path);
            }
        }
        return result;
    }

    private async Task<List<T>> ReadJsonlTailAsync<T>(string path, int limit, CancellationToken ct)
    {
        if (!File.Exists(path) || limit <= 0) return [];

        var queue = new Queue<T>(limit);
        await foreach (var line in File.ReadLinesAsync(path, ct).ConfigureAwait(false))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                var item = JsonSerializer.Deserialize<T>(line, _json);
                if (item is null)
                {
                    continue;
                }

                queue.Enqueue(item);
                if (queue.Count > limit)
                {
                    queue.Dequeue();
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Skipping malformed JSONL line in {Path}", path);
            }
        }

        return [.. queue];
    }

    private static async Task WriteJsonlAsync<T>(string path, IEnumerable<T> items, CancellationToken ct)
    {
        var lines = items.Select(i => JsonSerializer.Serialize(i, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        await File.WriteAllLinesAsync(path, lines, Encoding.UTF8, ct).ConfigureAwait(false);
    }

    private async Task TrimIfNeededAsync<T>(string path, int retention, CancellationToken ct)
    {
        var lines = await File.ReadAllLinesAsync(path, ct).ConfigureAwait(false);
        // Read all lines into memory to trim. For files bounded by 'retention' (default 500 lines)
        // this is intentionally acceptable; the total file size stays small.
        if (lines.Length <= retention) return;
        var trimmed = lines.TakeLast(retention);
        await File.WriteAllLinesAsync(path, trimmed, Encoding.UTF8, ct).ConfigureAwait(false);
    }

    private static string Sanitize(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(value.Select(c => invalid.Contains(c) ? '-' : c).ToArray());
    }
}
