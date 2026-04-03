using Garrard.Mcp.Explorer.Core.Domain.Connections;
using Garrard.Mcp.Explorer.Core.Domain.Preferences;
using Garrard.Mcp.Explorer.Core.Interfaces;
using Garrard.Mcp.Explorer.Infrastructure.Persistence;
using Moq;

namespace Garrard.Tests.Mcp.Explorer.Infrastructure.Persistence;

/// <summary>
/// Tests for <see cref="UserPreferencesStore"/>.
/// Each test uses a unique temp file path for isolation; the file is cleaned up in Dispose.
/// <see cref="ISecretProtector"/> is mocked as an identity function so the tests focus
/// on the persistence behaviour rather than encryption logic.
/// </summary>
public sealed class UserPreferencesStoreTests : IDisposable
{
    private readonly string _testDir;
    private readonly Mock<ISecretProtector> _protectorMock;

    public UserPreferencesStoreTests()
    {
        _testDir = Path.Combine(
            Path.GetTempPath(),
            "mcp-explorer-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);

        _protectorMock = new Mock<ISecretProtector>();
        // Identity: encrypt and decrypt return the same value
        _protectorMock.Setup(p => p.Encrypt(It.IsAny<string>())).Returns((string s) => s);
        _protectorMock.Setup(p => p.Decrypt(It.IsAny<string>())).Returns((string s) => s);
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, recursive: true); }
        catch { /* best-effort cleanup */ }
    }

    private UserPreferencesStore CreateStore(string? fileName = null)
    {
        var path = Path.Combine(_testDir, fileName ?? "settings.json");
        return new UserPreferencesStore(_protectorMock.Object, path);
    }

    // ── Load when file doesn't exist ──────────────────────────────────────────

    [Fact]
    public async Task LoadAsync_FileDoesNotExist_ReturnsDefaultPreferences()
    {
        var store = CreateStore();

        var prefs = await store.LoadAsync();

        Assert.NotNull(prefs);
        Assert.Equal("command-dark", prefs.Theme);
        Assert.Empty(prefs.Connections);
    }

    // ── Save → Load round-trip ────────────────────────────────────────────────

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_ReturnsSamePreferences()
    {
        var store = CreateStore();
        var original = new UserPreferences { Theme = "ocean-dark" };

        await store.SaveAsync(original);
        var loaded = await store.LoadAsync();

        Assert.Equal("ocean-dark", loaded.Theme);
    }

    [Fact]
    public async Task SaveAsync_Connections_ArePersistedAndLoaded()
    {
        var store = CreateStore();
        var prefs = new UserPreferences
        {
            Connections =
            [
                new ConnectionDefinition { Name = "server-a", Endpoint = "https://a.example.com" },
                new ConnectionDefinition { Name = "server-b", Endpoint = "https://b.example.com" }
            ]
        };

        await store.SaveAsync(prefs);
        var loaded = await store.LoadAsync();

        Assert.Equal(2, loaded.Connections.Count);
        Assert.Equal("server-a", loaded.Connections[0].Name);
        Assert.Equal("server-b", loaded.Connections[1].Name);
    }

    [Fact]
    public async Task SaveAsync_FavoritesList_IsPersisted()
    {
        var store = CreateStore();
        var prefs = new UserPreferences
        {
            FavoriteTools = ["tool-a", "tool-b", "tool-c"]
        };

        await store.SaveAsync(prefs);
        var loaded = await store.LoadAsync();

        Assert.Equal(3, loaded.FavoriteTools.Count);
        Assert.Contains("tool-a", loaded.FavoriteTools);
    }

    // ── StoragePath ───────────────────────────────────────────────────────────

    [Fact]
    public void StoragePath_ReturnsConfiguredFilePath()
    {
        var expectedPath = Path.Combine(_testDir, "my-settings.json");
        var store = new UserPreferencesStore(_protectorMock.Object, expectedPath);

        Assert.Equal(expectedPath, store.StoragePath);
    }

    // ── File is created on first save ─────────────────────────────────────────

    [Fact]
    public async Task SaveAsync_CreatesFileOnDisk()
    {
        var filePath = Path.Combine(_testDir, "created.json");
        var store = new UserPreferencesStore(_protectorMock.Object, filePath);

        await store.SaveAsync(new UserPreferences());

        Assert.True(File.Exists(filePath));
    }

    // ── Overwrite behaviour ───────────────────────────────────────────────────

    [Fact]
    public async Task SaveAsync_CalledTwice_SecondWriteWins()
    {
        var store = CreateStore();

        await store.SaveAsync(new UserPreferences { Theme = "forest-dark" });
        await store.SaveAsync(new UserPreferences { Theme = "ocean-light" });

        var loaded = await store.LoadAsync();

        Assert.Equal("ocean-light", loaded.Theme);
    }

    // ── Concurrent saves don't corrupt data ───────────────────────────────────

    [Fact]
    public async Task SaveAsync_ConcurrentCalls_DoNotCorruptFile()
    {
        // Each store instance uses a separate file to avoid cross-test interference
        // from the shared static SemaphoreSlim.
        var tasks = Enumerable.Range(0, 5).Select(async i =>
        {
            var store = CreateStore($"concurrent-{i}.json");
            var prefs = new UserPreferences { Theme = $"theme-{i}" };
            await store.SaveAsync(prefs);
            var loaded = await store.LoadAsync();
            return loaded.Theme;
        });

        var results = await Task.WhenAll(tasks);

        // All saves and loads must succeed (no exceptions, no null themes)
        Assert.All(results, theme => Assert.NotEmpty(theme));
    }

    // ── Null guard ────────────────────────────────────────────────────────────

    [Fact]
    public async Task SaveAsync_NullPreferences_ThrowsArgumentNullException()
    {
        var store = CreateStore();

        await Assert.ThrowsAsync<ArgumentNullException>(() => store.SaveAsync(null!));
    }

    // ── Sensitive field encryption is called for authorization headers ────────

    [Fact]
    public async Task SaveAsync_AuthorizationHeaderValue_IsEncrypted()
    {
        var store = CreateStore();
        var header = new ConnectionHeader
        {
            Name = "Authorization",
            Value = "secret-token",
            AuthorizationType = "Bearer"
        };
        var prefs = new UserPreferences
        {
            Connections = [new ConnectionDefinition { Name = "s", Headers = [header] }]
        };

        await store.SaveAsync(prefs);

        // Encrypt should have been called for the Authorization header value
        _protectorMock.Verify(p => p.Encrypt("secret-token"), Times.Once);
    }

    [Fact]
    public async Task LoadAsync_AuthorizationHeaderValue_IsDecrypted()
    {
        var store = CreateStore();
        var header = new ConnectionHeader
        {
            Name = "Authorization",
            Value = "secret-token",
            AuthorizationType = "Bearer"
        };
        var prefs = new UserPreferences
        {
            Connections = [new ConnectionDefinition { Name = "s", Headers = [header] }]
        };

        await store.SaveAsync(prefs);
        var loaded = await store.LoadAsync();

        // Verify Decrypt was called with the (identity-mock) encrypted value
        _protectorMock.Verify(p => p.Decrypt("secret-token"), Times.AtLeastOnce);
        // And the decrypted value is correctly returned in the loaded preferences
        Assert.Equal("secret-token", loaded.Connections[0].Headers[0].Value);
    }

    // ── Azure / OAuth / LLM key encryption ───────────────────────────────────

    [Fact]
    public async Task SaveAsync_AzureClientSecret_IsEncrypted()
    {
        var store = CreateStore();
        var prefs = new UserPreferences
        {
            Connections =
            [
                new ConnectionDefinition
                {
                    Name = "azure-server",
                    AuthenticationMode = ConnectionAuthenticationMode.AzureClientCredentials,
                    AzureCredentials = new()
                    {
                        TenantId = "tenant",
                        ClientId = "client",
                        ClientSecret = "azure-secret",
                        Scope = "https://management.azure.com/.default"
                    }
                }
            ]
        };

        await store.SaveAsync(prefs);

        _protectorMock.Verify(p => p.Encrypt("azure-secret"), Times.Once);
    }

    [Fact]
    public async Task SaveAsync_OAuthClientSecret_IsEncrypted()
    {
        var store = CreateStore();
        var prefs = new UserPreferences
        {
            Connections =
            [
                new ConnectionDefinition
                {
                    Name = "oauth-server",
                    AuthenticationMode = ConnectionAuthenticationMode.OAuth,
                    OAuthOptions = new()
                    {
                        ClientId = "client-id",
                        ClientSecret = "oauth-secret",
                        RedirectUri = "http://localhost/callback",
                        Scopes = "openid"
                    }
                }
            ]
        };

        await store.SaveAsync(prefs);

        _protectorMock.Verify(p => p.Encrypt("oauth-secret"), Times.Once);
    }
}
