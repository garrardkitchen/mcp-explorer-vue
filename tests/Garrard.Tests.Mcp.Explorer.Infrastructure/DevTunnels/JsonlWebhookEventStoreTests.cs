using Garrard.Mcp.Explorer.Core.Domain.DevTunnels;
using Garrard.Mcp.Explorer.Infrastructure.DevTunnels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Garrard.Tests.Mcp.Explorer.Infrastructure.DevTunnels;

public sealed class JsonlWebhookEventStoreTests : IDisposable
{
    private readonly string _testDir;

    public JsonlWebhookEventStoreTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "mcp-explorer-webhook-store-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, recursive: true); } catch { }
    }

    [Fact]
    public async Task AppendAsync_ThenGetRecentAsync_RoundTripsEvent()
    {
        var sut = CreateSut(retention: 10);
        var evt = CreateEvent("evt-1", receivedAtUtc: DateTime.UtcNow);

        await sut.AppendAsync(evt);

        var events = await sut.GetRecentAsync("tunnel-1");

        var actual = Assert.Single(events);
        Assert.Equal("evt-1", actual.Id);
        Assert.Equal("""{"hello":"world"}""", actual.BodyText);
    }

    [Fact]
    public async Task AppendAsync_AboveRetention_TrimsOldestEvents()
    {
        var sut = CreateSut(retention: 2);

        await sut.AppendAsync(CreateEvent("evt-1", DateTime.UtcNow.AddMinutes(-2)));
        await sut.AppendAsync(CreateEvent("evt-2", DateTime.UtcNow.AddMinutes(-1)));
        await sut.AppendAsync(CreateEvent("evt-3", DateTime.UtcNow));

        var events = await sut.GetRecentAsync("tunnel-1", limit: 10);

        Assert.Equal(2, events.Count);
        Assert.DoesNotContain(events, e => e.Id == "evt-1");
        Assert.Contains(events, e => e.Id == "evt-2");
        Assert.Contains(events, e => e.Id == "evt-3");
    }

    [Fact]
    public async Task GetAsync_ReturnsSingleEventById()
    {
        var sut = CreateSut(retention: 10);

        await sut.AppendAsync(CreateEvent("evt-1", DateTime.UtcNow.AddMinutes(-1)));
        await sut.AppendAsync(CreateEvent("evt-2", DateTime.UtcNow));

        var evt = await sut.GetAsync("tunnel-1", "evt-2");

        Assert.NotNull(evt);
        Assert.Equal("evt-2", evt!.Id);
    }

    [Fact]
    public async Task StreamAsync_PublishesAppendedEvents()
    {
        var sut = CreateSut(retention: 10);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

        var task = Task.Run(async () =>
        {
            await foreach (var evt in sut.StreamAsync("tunnel-1", cts.Token))
                return evt;

            return null;
        }, cts.Token);

        await Task.Delay(50, cts.Token);
        await sut.AppendAsync(CreateEvent("evt-1", DateTime.UtcNow), cts.Token);

        var evt = await task;

        Assert.NotNull(evt);
        Assert.Equal("evt-1", evt!.Id);
    }

    private JsonlWebhookEventStore CreateSut(int retention)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DevTunnels:DataPath"] = _testDir,
                ["DevTunnels:Retention"] = retention.ToString()
            })
            .Build();

        return new JsonlWebhookEventStore(configuration, NullLogger<JsonlWebhookEventStore>.Instance);
    }

    private static WebhookEvent CreateEvent(string id, DateTime receivedAtUtc)
        => new(
            Id: id,
            TunnelId: "tunnel-1",
            ReceivedAtUtc: receivedAtUtc,
            Method: "POST",
            Path: "/hooks/example",
            QueryString: "?x=1",
            Headers: new Dictionary<string, string> { ["content-type"] = "application/json" },
            ContentType: "application/json",
            BodySize: 17,
            BodyText: """{"hello":"world"}""",
            BodyBase64: null,
            ContentEncoding: "utf-8",
            RemoteIp: "127.0.0.1",
            Truncated: false);
}
