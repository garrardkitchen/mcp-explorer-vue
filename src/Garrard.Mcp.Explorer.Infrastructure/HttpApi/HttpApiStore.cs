using System.Text.Json;
using System.Text.Json.Serialization;
using Garrard.Mcp.Explorer.Core.Domain.HttpApi;
using Garrard.Mcp.Explorer.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Garrard.Mcp.Explorer.Infrastructure.HttpApi;

/// <summary>
/// Persists HTTP API definitions, collections, groups and favourites to
/// HttpApis/http-apis.json alongside the settings.json directory.
/// Thread-safe via a single SemaphoreSlim.
/// </summary>
public sealed class HttpApiStore : IHttpApiStore
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: true) }
    };

    private readonly string _filePath;
    private readonly ILogger<HttpApiStore> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public HttpApiStore(IConfiguration configuration, ILogger<HttpApiStore> logger)
    {
        _logger = logger;

        var customPath = configuration["PREFERENCES:StoragePath"]
                         ?? Environment.GetEnvironmentVariable("PREFERENCES__StoragePath");

        var dir = string.IsNullOrWhiteSpace(customPath)
            ? Path.Combine(
                OperatingSystem.IsWindows()
                    ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
                    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share"),
                "McpExplorer", "HttpApis")
            : Path.Combine(Path.GetDirectoryName(customPath)!, "HttpApis");

        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "http-apis.json");
    }

    // ── Definitions ──────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<HttpApiDefinition>> GetAllDefinitionsAsync(CancellationToken ct = default)
    {
        var data = await LoadAsync(ct).ConfigureAwait(false);
        return data.Definitions;
    }

    public async Task<HttpApiDefinition?> GetDefinitionAsync(string id, CancellationToken ct = default)
    {
        var data = await LoadAsync(ct).ConfigureAwait(false);
        return data.Definitions.FirstOrDefault(d => string.Equals(d.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<HttpApiDefinition> SaveDefinitionAsync(HttpApiDefinition definition, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var data = await LoadUnlockedAsync(ct).ConfigureAwait(false);
            var existing = data.Definitions.FirstOrDefault(d =>
                string.Equals(d.Id, definition.Id, StringComparison.OrdinalIgnoreCase));

            List<HttpApiDefinition> updated;
            if (existing is null)
            {
                updated = [..data.Definitions, definition];
            }
            else
            {
                definition.LastUpdatedAt = DateTime.UtcNow;
                updated = data.Definitions
                    .Select(d => string.Equals(d.Id, definition.Id, StringComparison.OrdinalIgnoreCase) ? definition : d)
                    .ToList();
            }

            await SaveUnlockedAsync(data with { Definitions = updated }, ct).ConfigureAwait(false);
            return definition;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task DeleteDefinitionAsync(string id, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var data = await LoadUnlockedAsync(ct).ConfigureAwait(false);
            var updated = data with
            {
                Definitions = data.Definitions.Where(d => !string.Equals(d.Id, id, StringComparison.OrdinalIgnoreCase)).ToList(),
                FavouriteIds = data.FavouriteIds.Where(f => !string.Equals(f, id, StringComparison.OrdinalIgnoreCase)).ToList()
            };
            await SaveUnlockedAsync(updated, ct).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    // ── Collections ──────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<HttpApiCollection>> GetAllCollectionsAsync(CancellationToken ct = default)
    {
        var data = await LoadAsync(ct).ConfigureAwait(false);
        return data.Collections;
    }

    public async Task<HttpApiCollection?> GetCollectionAsync(string id, CancellationToken ct = default)
    {
        var data = await LoadAsync(ct).ConfigureAwait(false);
        return data.Collections.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<HttpApiCollection> SaveCollectionAsync(HttpApiCollection collection, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var data = await LoadUnlockedAsync(ct).ConfigureAwait(false);
            var existing = data.Collections.FirstOrDefault(c =>
                string.Equals(c.Id, collection.Id, StringComparison.OrdinalIgnoreCase));

            List<HttpApiCollection> updated;
            if (existing is null)
            {
                updated = [..data.Collections, collection];
            }
            else
            {
                collection.LastUpdatedAt = DateTime.UtcNow;
                updated = data.Collections
                    .Select(c => string.Equals(c.Id, collection.Id, StringComparison.OrdinalIgnoreCase) ? collection : c)
                    .ToList();
            }

            await SaveUnlockedAsync(data with { Collections = updated }, ct).ConfigureAwait(false);
            return collection;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task DeleteCollectionAsync(string id, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var data = await LoadUnlockedAsync(ct).ConfigureAwait(false);
            var updated = data with
            {
                Collections = data.Collections.Where(c => !string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase)).ToList()
            };
            await SaveUnlockedAsync(updated, ct).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task PatchCollectionRunStatsAsync(
        string id,
        DateTime lastRunAt,
        long durationMs,
        int successCount,
        int totalCount,
        string runId,
        string invokedVia,
        List<HttpApiCollectionEndpointRunSummary> endpointSummaries,
        CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var data = await LoadUnlockedAsync(ct).ConfigureAwait(false);
            var updated = data.Collections.Select(c =>
            {
                if (!string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase)) return c;
                // Create a new instance to avoid mutating the shared object (thread safety)
                // LastUpdatedAt is intentionally not touched — this is a run-stats patch only
                return new HttpApiCollection
                {
                    Id                       = c.Id,
                    Name                     = c.Name,
                    Description              = c.Description,
                    EndpointIds              = c.EndpointIds,
                    GroupName                = c.GroupName,
                    CreatedAt                = c.CreatedAt,
                    LastUpdatedAt            = c.LastUpdatedAt,
                    LastRunAt                = lastRunAt,
                    LastRunDurationMs        = durationMs,
                    LastRunSuccessCount      = successCount,
                    LastRunTotalCount        = totalCount,
                    LastRunId                = runId,
                    LastRunInvokedVia        = invokedVia,
                    LastRunEndpointSummaries = endpointSummaries
                };
            }).ToList();
            await SaveUnlockedAsync(data with { Collections = updated }, ct).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    // ── Groups ───────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<HttpApiGroup>> GetAllGroupsAsync(CancellationToken ct = default)
    {
        var data = await LoadAsync(ct).ConfigureAwait(false);
        return data.Groups;
    }

    public async Task<HttpApiGroup> SaveGroupAsync(HttpApiGroup group, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var data = await LoadUnlockedAsync(ct).ConfigureAwait(false);
            var existing = data.Groups.FirstOrDefault(g =>
                string.Equals(g.Name, group.Name, StringComparison.OrdinalIgnoreCase));

            var updated = existing is null
                ? [..data.Groups, group]
                : data.Groups.Select(g => string.Equals(g.Name, group.Name, StringComparison.OrdinalIgnoreCase) ? group : g).ToList();

            await SaveUnlockedAsync(data with { Groups = updated }, ct).ConfigureAwait(false);
            return group;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task DeleteGroupAsync(string name, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var data = await LoadUnlockedAsync(ct).ConfigureAwait(false);
            var updated = data with
            {
                Groups = data.Groups.Where(g => !string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase)).ToList()
            };
            await SaveUnlockedAsync(updated, ct).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    // ── Favourites ───────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<string>> GetFavouriteIdsAsync(CancellationToken ct = default)
    {
        var data = await LoadAsync(ct).ConfigureAwait(false);
        return data.FavouriteIds;
    }

    public async Task SetFavouriteAsync(string id, bool isFavourite, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var data = await LoadUnlockedAsync(ct).ConfigureAwait(false);
            var favs = data.FavouriteIds.ToList();
            var already = favs.Any(f => string.Equals(f, id, StringComparison.OrdinalIgnoreCase));

            if (isFavourite && !already) favs.Add(id);
            if (!isFavourite && already) favs.RemoveAll(f => string.Equals(f, id, StringComparison.OrdinalIgnoreCase));

            await SaveUnlockedAsync(data with { FavouriteIds = favs }, ct).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<HttpApiStoreData> LoadAsync(CancellationToken ct)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try { return await LoadUnlockedAsync(ct).ConfigureAwait(false); }
        finally { _lock.Release(); }
    }

    private async Task<HttpApiStoreData> LoadUnlockedAsync(CancellationToken ct)
    {
        if (!File.Exists(_filePath)) return new HttpApiStoreData();
        try
        {
            var json = await File.ReadAllTextAsync(_filePath, ct).ConfigureAwait(false);
            return JsonSerializer.Deserialize<HttpApiStoreData>(json, _json) ?? new HttpApiStoreData();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load HTTP API store from {Path}", _filePath);
            return new HttpApiStoreData();
        }
    }

    private async Task SaveUnlockedAsync(HttpApiStoreData data, CancellationToken ct)
    {
        var dir = Path.GetDirectoryName(_filePath)!;
        Directory.CreateDirectory(dir);
        var tmp = _filePath + ".tmp";
        try
        {
            await using (var f = File.Create(tmp))
                await JsonSerializer.SerializeAsync(f, data, _json, ct).ConfigureAwait(false);
            File.Move(tmp, _filePath, overwrite: true);
        }
        catch
        {
            if (File.Exists(tmp)) try { File.Delete(tmp); } catch { /* best effort */ }
            throw;
        }
    }

    // ── Serialization model ───────────────────────────────────────────────────

    private sealed record HttpApiStoreData
    {
        public List<HttpApiDefinition> Definitions { get; init; } = [];
        public List<HttpApiCollection> Collections { get; init; } = [];
        public List<HttpApiGroup> Groups { get; init; } = [];
        public List<string> FavouriteIds { get; init; } = [];
    }
}
