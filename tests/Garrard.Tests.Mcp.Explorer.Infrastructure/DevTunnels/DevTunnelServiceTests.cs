using Garrard.Mcp.Explorer.Core.Domain.DevTunnels;
using Garrard.Mcp.Explorer.Core.Interfaces;
using Garrard.Mcp.Explorer.Infrastructure.DevTunnels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;

namespace Garrard.Tests.Mcp.Explorer.Infrastructure.DevTunnels;

public sealed class DevTunnelServiceTests : IDisposable
{
    private readonly string _testDir;

    public DevTunnelServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "mcp-explorer-devtunnel-service-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, recursive: true); } catch { }
    }

    [Fact]
    public async Task CaptureAsync_RedactsSensitiveHeaders_AndTruncatesBody()
    {
        var eventStore = new InMemoryWebhookEventStore();
        var sut = CreateSut(eventStore);
        var tunnel = await sut.CreateAsync(new CreateDevTunnelRequest("Payments", TunnelAccess.Anonymous, false));

        await sut.CaptureAsync(new WebhookCaptureRequest(
            TunnelId: tunnel.Id,
            Method: "POST",
            Path: "/hook",
            QueryString: string.Empty,
            Headers: new Dictionary<string, string>
            {
                ["Authorization"] = "Bearer secret",
                ["X-Trace-Id"] = "abc"
            },
            ContentType: "application/json",
            RemoteIp: "127.0.0.1",
            Body: Enumerable.Repeat((byte)'a', 20).ToArray()));

        var captured = Assert.Single(eventStore.Events);
        Assert.Equal("████", captured.Headers["Authorization"]);
        Assert.Equal("abc", captured.Headers["X-Trace-Id"]);
        Assert.True(captured.Truncated);
        Assert.Equal(20, captured.BodySize);
        Assert.Equal(8, captured.BodyText!.Length);

        await sut.StopAllAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ReplayAsync_UsesPinnedReplayAddresses()
    {
        var eventStore = new InMemoryWebhookEventStore();
        var sut = CreateSut(eventStore);
        var tunnel = await sut.CreateAsync(new CreateDevTunnelRequest("Replay", TunnelAccess.Anonymous, false));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        try
        {
            eventStore.Events.Add(new WebhookEvent(
                Id: "evt-1",
                TunnelId: tunnel.Id,
                ReceivedAtUtc: DateTime.UtcNow,
                Method: "POST",
                Path: "/hook",
                QueryString: string.Empty,
                Headers: new Dictionary<string, string> { ["content-type"] = "application/json" },
                ContentType: "application/json",
                BodySize: 17,
                BodyText: """{"hello":"world"}""",
                BodyBase64: null,
                ContentEncoding: "utf-8",
                RemoteIp: "127.0.0.1",
                Truncated: false));

            var serverTask = Task.Run(async () =>
            {
                using var client = await listener.AcceptTcpClientAsync();
                using var stream = client.GetStream();
                var response = Encoding.ASCII.GetBytes("HTTP/1.1 202 Accepted\r\nContent-Length: 2\r\n\r\nok");
                await stream.WriteAsync(response);
                await stream.FlushAsync();
            });

            var result = await sut.ReplayAsync(new ReplayWebhookRequest(
                TunnelId: tunnel.Id,
                EventId: "evt-1",
                TargetUri: new Uri($"http://replay.example:{((IPEndPoint)listener.LocalEndpoint).Port}/hook"),
                MethodOverride: null,
                HeadersOverride: null,
                BodyTextOverride: null,
                BodyBase64Override: null,
                ContentTypeOverride: null,
                AllowedAddresses: [IPAddress.Loopback]));

            Assert.Equal(202, result.StatusCode);
            Assert.Equal("ok", result.BodyText);

            await serverTask;
            await sut.StopAllAsync(CancellationToken.None);
        }
        finally
        {
            listener.Stop();
        }
    }

    private DevTunnelService CreateSut(IWebhookEventStore eventStore)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DevTunnels:DataPath"] = _testDir,
                ["DevTunnels:CapturePort"] = "5000",
                ["DevTunnels:MaxCaptureBytes"] = "8"
            })
            .Build();

        return new DevTunnelService(
            new FakeDevTunnelCli(),
            eventStore,
            new StaticHttpClientFactory(new HttpClient(new StubHttpMessageHandler())),
            configuration,
            NullLogger<DevTunnelService>.Instance);
    }

    private sealed class FakeDevTunnelCli : IDevTunnelCli
    {
        public Task<DevTunnelUserState> GetUserStateAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new DevTunnelUserState(true, "demo", "GitHub"));

        public IAsyncEnumerable<string> LoginWithDeviceCodeAsync(CancellationToken cancellationToken = default)
            => AsyncEnumerable.Empty<string>();

        public Task<string> EnsureTunnelExistsAsync(string tunnelName, TunnelAccess access, int port, string? existingCliTunnelId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(existingCliTunnelId ?? "cli-" + tunnelName);

        public async IAsyncEnumerable<DevTunnelHostUpdate> HostAsync(string tunnelId, TunnelAccess access, int port, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return new DevTunnelHostUpdate(DateTime.UtcNow, "Connect via browser: https://abc.devtunnels.ms/", TunnelStatus.Running, new Uri("https://abc.devtunnels.ms"));
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
        }

        public Task DeleteTunnelAsync(string tunnelId, TunnelAccess access, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class InMemoryWebhookEventStore : IWebhookEventStore
    {
        public List<WebhookEvent> Events { get; } = [];

        public Task AppendAsync(WebhookEvent webhookEvent, CancellationToken cancellationToken = default)
        {
            Events.Add(webhookEvent);
            return Task.CompletedTask;
        }

        public Task<WebhookEvent?> GetAsync(string tunnelId, string eventId, CancellationToken cancellationToken = default)
            => Task.FromResult<WebhookEvent?>(Events.FirstOrDefault(e => e.TunnelId == tunnelId && e.Id == eventId));

        public Task<IReadOnlyList<WebhookEvent>> GetRecentAsync(string tunnelId, int? limit = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<WebhookEvent>>(Events.Where(e => e.TunnelId == tunnelId).ToList());

        public async IAsyncEnumerable<WebhookEvent> StreamAsync(string tunnelId, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var evt in Events.Where(e => e.TunnelId == tunnelId))
                yield return evt;

            await Task.CompletedTask;
        }
    }

    private sealed class StaticHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}")
            });
    }
}
