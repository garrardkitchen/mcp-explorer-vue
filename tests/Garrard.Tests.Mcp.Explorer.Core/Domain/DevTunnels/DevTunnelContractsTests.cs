using System.Net;

using Garrard.Mcp.Explorer.Core.Domain.DevTunnels;

namespace Garrard.Tests.Mcp.Explorer.Core.Domain.DevTunnels;

public class DevTunnelContractsTests
{
    [Fact]
    public void DevTunnel_RecordStoresExpectedValues()
    {
        var createdAt = DateTime.UtcNow;
        var tunnelUri = new Uri("https://abc.devtunnels.ms");
        var webhookUri = new Uri("https://abc.devtunnels.ms/api/v1/webhooks/tunnel-1");

        var tunnel = new DevTunnel(
            Id: "tunnel-1",
            Name: "Local Webhook",
            Access: TunnelAccess.Anonymous,
            Status: TunnelStatus.Running,
            TunnelUri: tunnelUri,
            WebhookUri: webhookUri,
            CreatedAtUtc: createdAt,
            LastStartedAtUtc: createdAt,
            LastStoppedAtUtc: null,
            LastError: null,
            DeleteOnExit: true,
            RestartCount: 2);

        Assert.Equal("tunnel-1", tunnel.Id);
        Assert.Equal("Local Webhook", tunnel.Name);
        Assert.Equal(TunnelAccess.Anonymous, tunnel.Access);
        Assert.Equal(TunnelStatus.Running, tunnel.Status);
        Assert.Equal(tunnelUri, tunnel.TunnelUri);
        Assert.Equal(webhookUri, tunnel.WebhookUri);
        Assert.True(tunnel.DeleteOnExit);
        Assert.Equal(2, tunnel.RestartCount);
    }

    [Fact]
    public void WebhookEvent_RecordStoresTextAndBinaryMetadata()
    {
        var receivedAt = DateTime.UtcNow;
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["content-type"] = "application/json",
            ["x-source"] = "unit-test"
        };

        var evt = new WebhookEvent(
            Id: "evt-1",
            TunnelId: "tunnel-1",
            ReceivedAtUtc: receivedAt,
            Method: "POST",
            Path: "/callbacks/run",
            QueryString: "?page=1",
            Headers: headers,
            ContentType: "application/json",
            BodySize: 42,
            BodyText: """{"ok":true}""",
            BodyBase64: null,
            ContentEncoding: "utf-8",
            RemoteIp: "127.0.0.1",
            Truncated: false);

        Assert.Equal("evt-1", evt.Id);
        Assert.Equal("POST", evt.Method);
        Assert.Equal("/callbacks/run", evt.Path);
        Assert.Equal("?page=1", evt.QueryString);
        Assert.Equal("application/json", evt.ContentType);
        Assert.Equal("""{"ok":true}""", evt.BodyText);
        Assert.Null(evt.BodyBase64);
        Assert.False(evt.Truncated);
    }

    [Fact]
    public void CreateDevTunnelRequest_SupportsDeleteOnExitFlag()
    {
        var request = new CreateDevTunnelRequest(
            Name: "Payments",
            Access: TunnelAccess.Authenticated,
            DeleteOnExit: true);

        Assert.Equal("Payments", request.Name);
        Assert.Equal(TunnelAccess.Authenticated, request.Access);
        Assert.True(request.DeleteOnExit);
    }

    [Theory]
    [InlineData(TunnelAccess.Anonymous)]
    [InlineData(TunnelAccess.Authenticated)]
    public void TunnelAccess_EnumSupportsAllValues(TunnelAccess access)
    {
        var request = new CreateDevTunnelRequest("Name", access, DeleteOnExit: false);
        Assert.Equal(access, request.Access);
    }

    [Theory]
    [InlineData(TunnelStatus.Stopped)]
    [InlineData(TunnelStatus.Starting)]
    [InlineData(TunnelStatus.Running)]
    [InlineData(TunnelStatus.LoginRequired)]
    [InlineData(TunnelStatus.Error)]
    public void DevTunnelHostUpdate_CanRepresentEachTunnelStatus(TunnelStatus status)
    {
        var update = new DevTunnelHostUpdate(
            TimestampUtc: DateTime.UtcNow,
            OutputLine: "host line",
            Status: status,
            TunnelUri: new Uri("https://xyz.devtunnels.ms"));

        Assert.Equal(status, update.Status);
    }

    [Fact]
    public void ReplayWebhookRequest_StoresOverrideValues()
    {
        var request = new ReplayWebhookRequest(
            TunnelId: "t1",
            EventId: "e1",
            TargetUri: new Uri("https://example.com/replay"),
            MethodOverride: "PUT",
            HeadersOverride: new Dictionary<string, string> { ["x-test"] = "1" },
            BodyTextOverride: """{"retry":true}""",
            BodyBase64Override: null,
            ContentTypeOverride: "application/json",
            AllowedAddresses: [IPAddress.Parse("1.1.1.1")]);

        Assert.Equal("t1", request.TunnelId);
        Assert.Equal("e1", request.EventId);
        Assert.Equal("PUT", request.MethodOverride);
        Assert.Equal("application/json", request.ContentTypeOverride);
        Assert.Equal("""{"retry":true}""", request.BodyTextOverride);
        Assert.Single(request.AllowedAddresses);
    }
}
