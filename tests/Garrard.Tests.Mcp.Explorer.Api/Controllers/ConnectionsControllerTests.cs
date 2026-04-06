using Garrard.Mcp.Explorer.Api.Controllers.v1;
using Garrard.Mcp.Explorer.Api.Dtos.Connections;
using Garrard.Mcp.Explorer.Core.Domain.Connections;
using Garrard.Mcp.Explorer.Core.Domain.Preferences;
using Garrard.Mcp.Explorer.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Garrard.Tests.Mcp.Explorer.Api.Controllers;

/// <summary>
/// Unit tests for <see cref="ConnectionsController"/>.
/// Both <see cref="IConnectionService"/> and <see cref="IUserPreferencesStore"/> are mocked.
/// </summary>
public class ConnectionsControllerTests
{
    private readonly Mock<IConnectionService> _connectionServiceMock;
    private readonly Mock<IUserPreferencesStore> _storeMock;
    private readonly Mock<IConnectionExportService> _exportServiceMock;
    private readonly ConnectionsController _sut;

    public ConnectionsControllerTests()
    {
        _connectionServiceMock = new Mock<IConnectionService>();
        _storeMock = new Mock<IUserPreferencesStore>();
        _exportServiceMock = new Mock<IConnectionExportService>();
        _sut = new ConnectionsController(_connectionServiceMock.Object, _storeMock.Object, _exportServiceMock.Object);
    }

    // ── GET /connections ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_Returns200WithConnectionList()
    {
        var connections = new List<ConnectionDefinition>
        {
            new() { Name = "server-a", Endpoint = "https://a.example.com" },
            new() { Name = "server-b", Endpoint = "https://b.example.com" }
        };
        _storeMock.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new UserPreferences { Connections = connections });

        var result = await _sut.GetAll(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, ok.StatusCode);
        var returned = Assert.IsAssignableFrom<List<ConnectionDefinition>>(ok.Value);
        Assert.Equal(2, returned.Count);
    }

    [Fact]
    public async Task GetAll_NoConnections_ReturnsEmptyList()
    {
        _storeMock.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new UserPreferences());

        var result = await _sut.GetAll(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var returned = Assert.IsAssignableFrom<List<ConnectionDefinition>>(ok.Value);
        Assert.Empty(returned);
    }

    // ── POST /connections ─────────────────────────────────────────────────────

    [Fact]
    public async Task Create_Returns201CreatedAtAction()
    {
        var request = new CreateConnectionRequest("new-server", "https://new.example.com", null, null);
        _storeMock.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new UserPreferences());

        var result = await _sut.Create(request, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(201, created.StatusCode);
    }

    [Fact]
    public async Task Create_SavesNewConnectionToStore()
    {
        var request = new CreateConnectionRequest("my-server", "https://mcp.example.com", "note", "grp");
        _storeMock.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new UserPreferences());

        await _sut.Create(request, CancellationToken.None);

        _storeMock.Verify(
            s => s.SaveAsync(
                It.Is<UserPreferences>(p =>
                    p.Connections.Count == 1 &&
                    p.Connections[0].Name == "my-server" &&
                    p.Connections[0].Endpoint == "https://mcp.example.com"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Create_ReturnsConnectionDefinitionInBody()
    {
        var request = new CreateConnectionRequest("server-c", "https://c.example.com", null, null);
        _storeMock.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new UserPreferences());

        var result = await _sut.Create(request, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var def = Assert.IsType<ConnectionDefinition>(created.Value);
        Assert.Equal("server-c", def.Name);
        Assert.Equal("https://c.example.com", def.Endpoint);
    }

    [Fact]
    public async Task Create_AppendedToExistingConnections()
    {
        var existing = new ConnectionDefinition { Name = "existing" };
        var request = new CreateConnectionRequest("new-one", "https://new.example.com", null, null);
        _storeMock.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new UserPreferences { Connections = [existing] });

        await _sut.Create(request, CancellationToken.None);

        _storeMock.Verify(
            s => s.SaveAsync(
                It.Is<UserPreferences>(p => p.Connections.Count == 2),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── DELETE /connections/{name} ────────────────────────────────────────────

    [Fact]
    public async Task Delete_ExistingConnection_Returns204()
    {
        var connections = new List<ConnectionDefinition>
        {
            new() { Name = "to-delete" },
            new() { Name = "keep-me" }
        };
        _storeMock.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new UserPreferences { Connections = connections });

        var result = await _sut.Delete("to-delete", CancellationToken.None);

        var noContent = Assert.IsType<NoContentResult>(result);
        Assert.Equal(204, noContent.StatusCode);
    }

    [Fact]
    public async Task Delete_RemovesOnlyTheNamedConnection()
    {
        var connections = new List<ConnectionDefinition>
        {
            new() { Name = "remove-me" },
            new() { Name = "keep-me" }
        };
        _storeMock.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new UserPreferences { Connections = connections });

        await _sut.Delete("remove-me", CancellationToken.None);

        _storeMock.Verify(
            s => s.SaveAsync(
                It.Is<UserPreferences>(p =>
                    p.Connections.Count == 1 &&
                    p.Connections[0].Name == "keep-me"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Delete_NonExistentConnection_StillReturns204()
    {
        _storeMock.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new UserPreferences());

        var result = await _sut.Delete("does-not-exist", CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    // ── POST /connections/{name}/connect ──────────────────────────────────────

    [Fact]
    public async Task Connect_KnownConnection_Returns200()
    {
        var definition = new ConnectionDefinition { Name = "mcp-server", Endpoint = "https://mcp.example.com" };
        _storeMock.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new UserPreferences { Connections = [definition] });

        var activeConn = CreateActiveConnectionMock("mcp-server", "https://mcp.example.com", toolCount: 3);
        _connectionServiceMock
            .Setup(cs => cs.ConnectAsync(definition, It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeConn);

        var result = await _sut.Connect("mcp-server", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, ok.StatusCode);
    }

    [Fact]
    public async Task Connect_KnownConnection_ReturnsConnectionSummary()
    {
        var definition = new ConnectionDefinition { Name = "mcp-server", Endpoint = "https://mcp.example.com" };
        _storeMock.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new UserPreferences { Connections = [definition] });

        var activeConn = CreateActiveConnectionMock("mcp-server", "https://mcp.example.com", toolCount: 5);
        _connectionServiceMock
            .Setup(cs => cs.ConnectAsync(definition, It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeConn);

        var result = await _sut.Connect("mcp-server", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var value = ok.Value!;
        var name = value.GetType().GetProperty("name")?.GetValue(value) as string;
        var toolCount = (int?)value.GetType().GetProperty("toolCount")?.GetValue(value);
        Assert.Equal("mcp-server", name);
        Assert.Equal(5, toolCount);
    }

    [Fact]
    public async Task Connect_UnknownConnection_Returns404()
    {
        _storeMock.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new UserPreferences());

        var result = await _sut.Connect("unknown-server", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Connect_CallsConnectionService_WithCorrectDefinition()
    {
        var definition = new ConnectionDefinition { Name = "my-mcp", Endpoint = "https://mcp.example.com" };
        _storeMock.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new UserPreferences { Connections = [definition] });

        var activeConn = CreateActiveConnectionMock("my-mcp", "https://mcp.example.com", toolCount: 0);
        _connectionServiceMock
            .Setup(cs => cs.ConnectAsync(It.IsAny<ConnectionDefinition>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeConn);

        await _sut.Connect("my-mcp", CancellationToken.None);

        _connectionServiceMock.Verify(
            cs => cs.ConnectAsync(
                It.Is<ConnectionDefinition>(d => d.Name == "my-mcp"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── POST /connections/{name}/disconnect ───────────────────────────────────

    [Fact]
    public async Task Disconnect_Returns204()
    {
        var result = await _sut.Disconnect("server-x", CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task Disconnect_CallsConnectionServiceDisconnect()
    {
        await _sut.Disconnect("server-x", CancellationToken.None);

        _connectionServiceMock.Verify(
            cs => cs.DisconnectAsync("server-x", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── GET /connections/active ───────────────────────────────────────────────

    [Fact]
    public void GetActive_Returns200WithActiveConnections()
    {
        var active = new List<IActiveConnection>
        {
            CreateActiveConnectionMock("server-a", "https://a.example.com", 2),
            CreateActiveConnectionMock("server-b", "https://b.example.com", 0)
        };
        _connectionServiceMock.Setup(cs => cs.GetActiveConnections())
                               .Returns(active);

        var result = _sut.GetActive();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, ok.StatusCode);
    }

    // ── POST /connections/export ──────────────────────────────────────────────

    [Fact]
    public async Task Export_NoPassword_Returns400()
    {
        var req = new ExportConnectionsRequest(["conn-a"], "");
        var result = await _sut.Export(req, CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Export_NoNames_Returns400()
    {
        var req = new ExportConnectionsRequest([], "secret");
        var result = await _sut.Export(req, CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Export_ValidRequest_ReturnsFile()
    {
        var conn = new ConnectionDefinition { Name = "conn-a", Endpoint = "http://a.example.com" };
        _storeMock.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new UserPreferences { Connections = [conn] });
        _exportServiceMock.Setup(e => e.Encrypt(It.IsAny<IReadOnlyList<ConnectionDefinition>>(), "secret"))
                          .Returns(new ConnectionExportPayload { Salt = "s", Nonce = "n", Data = "d" });

        var req    = new ExportConnectionsRequest(["conn-a"], "secret");
        var result = await _sut.Export(req, CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/json", file.ContentType);
        Assert.Equal("connections-export.json", file.FileDownloadName);
    }

    // ── POST /connections/import ──────────────────────────────────────────────

    [Fact]
    public async Task Import_NoPassword_Returns400()
    {
        var payload = new ExportPayloadDto(1, "s", "n", "d");
        var req     = new ImportConnectionsRequest(payload, "");
        var result  = await _sut.Import(req, CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Import_WrongPassword_Returns400WithMessage()
    {
        var payload = new ExportPayloadDto(1, "s", "n", "d");
        var req     = new ImportConnectionsRequest(payload, "wrong-pw");
        _exportServiceMock
            .Setup(e => e.Decrypt(It.IsAny<ConnectionExportPayload>(), "wrong-pw"))
            .Throws(new InvalidOperationException("Incorrect password or corrupted file."));

        var result = await _sut.Import(req, CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(bad.Value);
    }

    [Fact]
    public async Task Import_ValidPayload_ImportsAndReturnsCount()
    {
        var existing = new ConnectionDefinition { Name = "existing", Endpoint = "http://e.example.com" };
        var incoming = new ConnectionDefinition { Name = "new-conn", Endpoint = "http://new.example.com" };

        _storeMock.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new UserPreferences { Connections = [existing] });
        _storeMock.Setup(s => s.SaveAsync(It.IsAny<UserPreferences>(), It.IsAny<CancellationToken>()))
                  .Returns(Task.CompletedTask);
        _exportServiceMock
            .Setup(e => e.Decrypt(It.IsAny<ConnectionExportPayload>(), "pw"))
            .Returns([incoming]);

        var payload = new ExportPayloadDto(1, "s", "n", "d");
        var req     = new ImportConnectionsRequest(payload, "pw");
        var result  = await _sut.Import(req, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task Import_NameCollision_AddsVersionSuffix()
    {
        var existing  = new ConnectionDefinition { Name = "my-conn", Endpoint = "http://old.example.com" };
        var duplicate = new ConnectionDefinition { Name = "my-conn", Endpoint = "http://new.example.com" };

        _storeMock.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new UserPreferences { Connections = [existing] });
        UserPreferences? saved = null;
        _storeMock.Setup(s => s.SaveAsync(It.IsAny<UserPreferences>(), It.IsAny<CancellationToken>()))
                  .Callback<UserPreferences, CancellationToken>((p, _) => saved = p)
                  .Returns(Task.CompletedTask);
        _exportServiceMock
            .Setup(e => e.Decrypt(It.IsAny<ConnectionExportPayload>(), "pw"))
            .Returns([duplicate]);

        await _sut.Import(new ImportConnectionsRequest(new ExportPayloadDto(1, "s", "n", "d"), "pw"), CancellationToken.None);

        Assert.NotNull(saved);
        Assert.Contains(saved!.Connections, c => c.Name == "my-conn (v2)");
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private static IActiveConnection CreateActiveConnectionMock(
        string name, string endpoint, int toolCount)
    {
        var mock = new Mock<IActiveConnection>();
        mock.Setup(c => c.Name).Returns(name);
        mock.Setup(c => c.Endpoint).Returns(endpoint);
        mock.Setup(c => c.IsConnected).Returns(true);
        mock.Setup(c => c.Tools).Returns(
            Enumerable.Range(0, toolCount)
                .Select(i => new ActiveTool($"tool-{i}", "desc", null, null))
                .ToList()
                .AsReadOnly());
        mock.Setup(c => c.Prompts).Returns(new List<ActivePrompt>().AsReadOnly());
        mock.Setup(c => c.Resources).Returns(new List<ActiveResource>().AsReadOnly());
        mock.Setup(c => c.ResourceTemplates).Returns(new List<ActiveResourceTemplate>().AsReadOnly());
        return mock.Object;
    }
}
