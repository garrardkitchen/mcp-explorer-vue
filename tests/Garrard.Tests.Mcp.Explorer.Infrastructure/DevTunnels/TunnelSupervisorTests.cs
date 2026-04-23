using Garrard.Mcp.Explorer.Core.Domain.DevTunnels;
using Garrard.Mcp.Explorer.Core.Interfaces;
using Garrard.Mcp.Explorer.Infrastructure.DevTunnels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Net.Http;

namespace Garrard.Tests.Mcp.Explorer.Infrastructure.DevTunnels;

public sealed class TunnelSupervisorTests : IDisposable
{
    private readonly string _testDir;

    public TunnelSupervisorTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "mcp-explorer-tunnel-supervisor-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, recursive: true); } catch { }
    }

    [Fact]
    public async Task StopAsync_DeletesTunnelsMarkedDeleteOnExit()
    {
        var cli = new FakeDevTunnelCli();
        var service = CreateService(cli);
        var sut = new TunnelSupervisor(service, NullLogger<TunnelSupervisor>.Instance);

        var ephemeral = await service.CreateAsync(new CreateDevTunnelRequest("Ephemeral", TunnelAccess.Anonymous, true));
        var persistent = await service.CreateAsync(new CreateDevTunnelRequest("Persistent", TunnelAccess.Anonymous, false));

        await sut.StopAsync(CancellationToken.None);

        var tunnels = await service.ListAsync(CancellationToken.None);
        Assert.DoesNotContain(tunnels, tunnel => tunnel.Id == ephemeral.Id);
        Assert.Contains(tunnels, tunnel => tunnel.Id == persistent.Id);
        Assert.Contains(ephemeral.CliTunnelId ?? ephemeral.Id, cli.DeletedTunnelIds);
    }

    private DevTunnelService CreateService(FakeDevTunnelCli cli)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DevTunnels:DataPath"] = _testDir,
                ["DevTunnels:CapturePort"] = "5000"
            })
            .Build();

        return new DevTunnelService(
            cli,
            new InMemoryWebhookEventStore(),
            new StaticHttpClientFactory(new HttpClient(new StubHttpMessageHandler())),
            configuration,
            NullLogger<DevTunnelService>.Instance);
    }

    private sealed class FakeDevTunnelCli : IDevTunnelCli
    {
        public List<string> DeletedTunnelIds { get; } = [];

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
        {
            DeletedTunnelIds.Add(tunnelId);
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryWebhookEventStore : IWebhookEventStore
    {
        public Task AppendAsync(WebhookEvent webhookEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<WebhookEvent?> GetAsync(string tunnelId, string eventId, CancellationToken cancellationToken = default) => Task.FromResult<WebhookEvent?>(null);
        public Task<IReadOnlyList<WebhookEvent>> GetRecentAsync(string tunnelId, int? limit = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<WebhookEvent>>([]);

        public async IAsyncEnumerable<WebhookEvent> StreamAsync(string tunnelId, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
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
