using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Garrard.Mcp.Explorer.Core.Domain.Chat;
using Garrard.Mcp.Explorer.Core.Domain.Preferences;
using Garrard.Mcp.Explorer.Core.Interfaces;

namespace Garrard.Mcp.Explorer.Infrastructure.Persistence;

/// <summary>
/// Thread-safe JSON persistence for <see cref="UserPreferences"/>.
/// Sensitive fields (API keys, secrets) are encrypted at rest via <see cref="ISecretProtector"/>.
/// Handles v1 settings.json field name migration transparently on load.
/// </summary>
public sealed class UserPreferencesStore : IUserPreferencesStore
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateOptions();

    private static JsonSerializerOptions CreateOptions() => new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        // Serialize/deserialize enums as PascalCase strings; allowIntegerValues:true handles v1 int-encoded enums
        Converters = { new JsonStringEnumConverter(allowIntegerValues: true) }
    };

    private readonly string _directory;
    private readonly string _filePath;
    private readonly ISecretProtector _protector;
    private static readonly SemaphoreSlim Lock = new(1, 1);

    public string StoragePath => _filePath;

    public UserPreferencesStore(ISecretProtector protector, string? customPath = null)
    {
        _protector = protector;

        if (customPath is not null)
        {
            _directory = Path.GetDirectoryName(customPath)!;
            _filePath = customPath;
        }
        else
        {
            var appData = OperatingSystem.IsWindows()
                ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");

            _directory = Path.Combine(appData, "McpExplorer");
            _filePath = Path.Combine(_directory, "settings.json");
        }
    }

    public async Task<UserPreferences> LoadAsync(CancellationToken cancellationToken = default)
    {
        await Lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(_filePath))
                return new UserPreferences();

            var rawJson = await File.ReadAllTextAsync(_filePath, cancellationToken).ConfigureAwait(false);
            var normalizedJson = NormalizeV1Json(rawJson);
            var raw = JsonSerializer.Deserialize<UserPreferences>(normalizedJson, SerializerOptions);

            if (raw is null) return new UserPreferences();

            var decrypted = PreferencesMapper.Decrypt(raw, _protector);

            // One-time migration: import v1 chat sessions stored as individual files
            // in a ChatSessions/ sibling directory into the v2 chatSessions list.
            // Migrated sessions are immediately persisted to disk so they survive restarts.
            // The marker file prevents re-scanning the directory on subsequent startups.
            var migrationFlag = Path.Combine(_directory, ".v1-migration-complete");
            if (!File.Exists(migrationFlag))
            {
                decrypted = MigrateV1ChatSessions(decrypted, _directory, migrationFlag);
                // Persist within the same lock — calling SaveAsync would deadlock.
                await WriteUnlockedAsync(decrypted, cancellationToken).ConfigureAwait(false);
            }

            var paramsMigrationFlag = Path.Combine(_directory, ".v1-params-migration-complete");
            if (!File.Exists(paramsMigrationFlag))
            {
                decrypted = BackfillV1ToolCallParameters(decrypted, _directory, paramsMigrationFlag);
                await WriteUnlockedAsync(decrypted, cancellationToken).ConfigureAwait(false);
            }

            return decrypted;
        }
        finally
        {
            Lock.Release();
        }
    }

    /// <summary>
    /// Renames v1-era field names so both old and new settings files deserialise correctly.
    /// Uses structured JSON manipulation rather than string replacement to avoid false matches
    /// inside string values (e.g. a note that mentions "azureClientCredentials").
    /// </summary>
    private static string NormalizeV1Json(string json)
    {
        var root = System.Text.Json.Nodes.JsonNode.Parse(json) as System.Text.Json.Nodes.JsonObject;
        if (root is null) return json;

        // Top-level field rename
        RenameJsonField(root, "selectedConnection", "selectedConnectionName");

        // Per-connection field renames
        if (root["connections"] is System.Text.Json.Nodes.JsonArray connections)
        {
            foreach (var item in connections)
            {
                if (item is not System.Text.Json.Nodes.JsonObject conn) continue;
                RenameJsonField(conn, "azureClientCredentials", "azureCredentials");
                RenameJsonField(conn, "oAuthCredentials", "oAuthOptions");
            }
        }

        return root.ToJsonString();
    }

    private static void RenameJsonField(System.Text.Json.Nodes.JsonObject obj, string oldKey, string newKey)
    {
        if (obj.TryGetPropertyValue(oldKey, out var value) && value is not null)
        {
            obj.Remove(oldKey);
            obj[newKey] = value?.DeepClone();
        }
    }

    public async Task SaveAsync(UserPreferences preferences, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        Directory.CreateDirectory(_directory);

        await Lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await WriteUnlockedAsync(preferences, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Lock.Release();
        }
    }

    /// <summary>
    /// Writes preferences to disk. Must only be called while <see cref="Lock"/> is held.
    /// </summary>
    private async Task WriteUnlockedAsync(UserPreferences preferences, CancellationToken cancellationToken)
    {
        var toWrite = PreferencesMapper.Encrypt(preferences, _protector);
        var tempPath = _filePath + ".tmp";

        try
        {
            await using (var temp = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(temp, toWrite, SerializerOptions, cancellationToken)
                    .ConfigureAwait(false);
            }

            File.Move(tempPath, _filePath, overwrite: true);
        }
        catch
        {
            if (File.Exists(tempPath))
                try { File.Delete(tempPath); } catch { /* best-effort cleanup */ }
            throw;
        }
    }

    /// <summary>
    /// Imports v1 chat sessions from individual JSON files in a ChatSessions/ subdirectory
    /// alongside the settings file. Only runs when no v2 sessions exist yet.
    /// </summary>
    private static UserPreferences MigrateV1ChatSessions(UserPreferences prefs, string directory, string migrationFlag)
    {
        // Write the marker first — even if no files exist, we don't want to re-scan on every startup.
        try { File.WriteAllText(migrationFlag, DateTimeOffset.UtcNow.ToString("O")); } catch { /* best effort */ }

        var chatSessionsDir = Path.Combine(directory, "ChatSessions");
        if (!Directory.Exists(chatSessionsDir)) return prefs;

        var files = Directory.GetFiles(chatSessionsDir, "*.json");
        if (files.Length == 0) return prefs;

        var migrated = new List<ChatSession>();
        var v1Options = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        foreach (var file in files)
        {
            try
            {
                var text = File.ReadAllText(file);
                var v1 = JsonSerializer.Deserialize<V1ChatSession>(text, v1Options);
                if (v1 is null) continue;

                var session = new ChatSession
                {
                    Id = v1.Id ?? Guid.NewGuid().ToString(),
                    Name = v1.Name ?? "Imported Chat",
                    CreatedAtUtc = v1.CreatedAt ?? DateTime.UtcNow,
                    LastActivityUtc = v1.LastAccessedAt ?? DateTime.UtcNow,
                };

                foreach (var m in v1.Messages ?? [])
                {
                    session.Messages.Add(new ChatMessage
                    {
                        Id = Guid.NewGuid().ToString(),
                        Role = m.Role ?? "user",
                        Content = m.Content ?? string.Empty,
                        TimestampUtc = m.Timestamp ?? DateTime.UtcNow,
                        ToolCallName = m.ToolName,
                        ToolCallParameters = m.Parameters,
                        ConnectionName = m.ConnectionName,
                        ModelName = m.ModelDisplayName,
                        ThinkingMilliseconds = m.ThinkingMilliseconds,
                        TokenUsage = (m.InputTokens > 0 || m.OutputTokens > 0)
                            ? new ChatTokenUsage(
                                m.InputTokens ?? 0,
                                m.OutputTokens ?? 0,
                                m.TotalTokens ?? ((m.InputTokens ?? 0) + (m.OutputTokens ?? 0)))
                            : null,
                    });
                }

                migrated.Add(session);
            }
            catch
            {
                // Skip malformed session files — best effort migration
            }
        }

        if (migrated.Count == 0) return prefs;

        // Sort by last activity descending (most recent first)
        migrated.Sort((a, b) => b.LastActivityUtc.CompareTo(a.LastActivityUtc));
        return prefs with { ChatSessions = migrated };
    }

    private static UserPreferences BackfillV1ToolCallParameters(UserPreferences prefs, string directory, string migrationFlag)
    {
        try { File.WriteAllText(migrationFlag, DateTimeOffset.UtcNow.ToString("O")); } catch { /* best effort */ }

        var chatSessionsDir = Path.Combine(directory, "ChatSessions");
        if (!Directory.Exists(chatSessionsDir)) return prefs;

        var v1Options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var updatedSessions = new List<ChatSession>();
        var anyUpdated = false;

        foreach (var session in prefs.ChatSessions)
        {
            var filePath = Path.Combine(chatSessionsDir, $"{session.Id}.json");
            if (!File.Exists(filePath))
            {
                updatedSessions.Add(session);
                continue;
            }

            // Only process sessions that have at least one tool call missing parameters
            var needsFill = session.Messages.Any(m => m.ToolCallName is not null && m.ToolCallParameters is null);
            if (!needsFill)
            {
                updatedSessions.Add(session);
                continue;
            }

            try
            {
                var text = File.ReadAllText(filePath);
                var v1 = JsonSerializer.Deserialize<V1ChatSession>(text, v1Options);
                if (v1?.Messages is null) { updatedSessions.Add(session); continue; }

                // Build ordered queue of v1 tool call messages for sequential matching
                var v1Queue = new Queue<V1ChatMessage>(
                    v1.Messages.Where(m => m.IsToolCall && m.ToolName is not null));

                var updatedMessages = new List<ChatMessage>();
                foreach (var msg in session.Messages)
                {
                    if (msg.ToolCallName is not null && msg.ToolCallParameters is null && v1Queue.Count > 0)
                    {
                        // Find the next matching v1 call; skip non-matching ones
                        V1ChatMessage? match = null;
                        var skipped = new List<V1ChatMessage>();
                        while (v1Queue.Count > 0)
                        {
                            var candidate = v1Queue.Dequeue();
                            if (string.Equals(candidate.ToolName, msg.ToolCallName, StringComparison.OrdinalIgnoreCase))
                            {
                                match = candidate;
                                break;
                            }
                            skipped.Add(candidate);
                        }
                        // Re-enqueue unmatched items at front (not possible with Queue, so rebuild)
                        if (skipped.Count > 0)
                        {
                            var temp = new List<V1ChatMessage>(skipped);
                            temp.AddRange(v1Queue);
                            v1Queue.Clear();
                            foreach (var item in temp) v1Queue.Enqueue(item);
                        }

                        if (match is not null)
                        {
                            updatedMessages.Add(new ChatMessage
                            {
                                Id = msg.Id,
                                Role = msg.Role,
                                Content = msg.Content,
                                TimestampUtc = msg.TimestampUtc,
                                ToolCallName = msg.ToolCallName,
                                ToolCallParameters = match.Parameters,
                                ConnectionName = msg.ConnectionName ?? match.ConnectionName,
                                ModelName = msg.ModelName,
                                TokenUsage = msg.TokenUsage,
                                ThinkingMilliseconds = msg.ThinkingMilliseconds,
                            });
                            anyUpdated = true;
                            continue;
                        }
                    }
                    updatedMessages.Add(msg);
                }

                updatedSessions.Add(new ChatSession
                {
                    Id = session.Id,
                    Name = session.Name,
                    CreatedAtUtc = session.CreatedAtUtc,
                    LastActivityUtc = session.LastActivityUtc,
                    Messages = { },
                });
                updatedSessions[^1].Messages.AddRange(updatedMessages);
            }
            catch
            {
                updatedSessions.Add(session);
            }
        }

        return anyUpdated ? prefs with { ChatSessions = updatedSessions } : prefs;
    }

    // ── V1 session file DTOs ─────────────────────────────────────────────────

    private sealed class V1ChatSession
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? LastAccessedAt { get; set; }
        public List<V1ChatMessage>? Messages { get; set; }
        public string? SelectedModelName { get; set; }
    }

    private sealed class V1ChatMessage
    {
        public string? Role { get; set; }
        public string? Content { get; set; }
        public DateTime? Timestamp { get; set; }
        public bool IsToolCall { get; set; }
        public string? ToolName { get; set; }
        public string? Parameters { get; set; }
        public string? ConnectionName { get; set; }
        public string? ModelDisplayName { get; set; }
        public int? InputTokens { get; set; }
        public int? OutputTokens { get; set; }
        public int? TotalTokens { get; set; }
        public int? ThinkingMilliseconds { get; set; }
    }
}
