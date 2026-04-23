using System.Text;
using System.Text.Json;
using System.Net;
using System.Net.Sockets;
using Asp.Versioning;
using Garrard.Mcp.Explorer.Api.Dtos.DevTunnels;
using Garrard.Mcp.Explorer.Core.Domain.DevTunnels;
using Garrard.Mcp.Explorer.Core.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Garrard.Mcp.Explorer.Api.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/devtunnels")]
public sealed class DevTunnelsController(IDevTunnelService devTunnelService) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
        => Ok(await devTunnelService.ListAsync(cancellationToken).ConfigureAwait(false));

    [HttpGet("{tunnelId}")]
    public async Task<IActionResult> Get(string tunnelId, CancellationToken cancellationToken)
    {
        var tunnel = await devTunnelService.GetAsync(tunnelId, cancellationToken).ConfigureAwait(false);
        return tunnel is null ? NotFound() : Ok(tunnel);
    }

    [HttpGet("user")]
    public async Task<IActionResult> GetUserState(CancellationToken cancellationToken)
        => Ok(await devTunnelService.GetUserStateAsync(cancellationToken).ConfigureAwait(false));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDevTunnelDto request, CancellationToken cancellationToken)
    {
        try
        {
            var tunnel = await devTunnelService.CreateAsync(
                new CreateDevTunnelRequest(request.Name, request.Access, request.DeleteOnExit),
                cancellationToken).ConfigureAwait(false);

            return CreatedAtAction(nameof(Get), new { tunnelId = tunnel.Id, version = "1.0" }, tunnel);
        }
        catch (DevTunnelCliUnavailableException ex)
        {
            return DevTunnelCliUnavailable(ex);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpPost("{tunnelId}/start")]
    public async Task<IActionResult> Start(string tunnelId, CancellationToken cancellationToken)
    {
        try
        {
            var tunnel = await devTunnelService.StartAsync(tunnelId, cancellationToken).ConfigureAwait(false);
            return tunnel is null ? NotFound() : Ok(tunnel);
        }
        catch (DevTunnelCliUnavailableException ex)
        {
            return DevTunnelCliUnavailable(ex);
        }
    }

    [HttpPost("{tunnelId}/stop")]
    public async Task<IActionResult> Stop(string tunnelId, CancellationToken cancellationToken)
    {
        var tunnel = await devTunnelService.StopAsync(tunnelId, cancellationToken).ConfigureAwait(false);
        return tunnel is null ? NotFound() : Ok(tunnel);
    }

    [HttpDelete("{tunnelId}")]
    public async Task<IActionResult> Delete(string tunnelId, CancellationToken cancellationToken)
    {
        try
        {
            var deleted = await devTunnelService.DeleteAsync(tunnelId, cancellationToken).ConfigureAwait(false);
            return deleted ? NoContent() : NotFound();
        }
        catch (DevTunnelCliUnavailableException ex)
        {
            return DevTunnelCliUnavailable(ex);
        }
    }

    [HttpGet("{tunnelId}/events")]
    public async Task<IActionResult> GetEvents(string tunnelId, [FromQuery] int? limit, CancellationToken cancellationToken)
    {
        var tunnel = await devTunnelService.GetAsync(tunnelId, cancellationToken).ConfigureAwait(false);
        if (tunnel is null)
            return NotFound();

        return Ok(await devTunnelService.GetEventsAsync(tunnelId, limit, cancellationToken).ConfigureAwait(false));
    }

    [HttpPost("{tunnelId}/replay/{eventId}")]
    public async Task<IActionResult> Replay(string tunnelId, string eventId, [FromBody] ReplayWebhookDto request, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(request.TargetUrl, UriKind.Absolute, out var targetUri))
            return BadRequest(new { error = "TargetUrl must be a valid absolute URI." });

        var allowedAddresses = await ResolveReplayAddressesAsync(targetUri, cancellationToken).ConfigureAwait(false);
        if (allowedAddresses is null)
            return BadRequest(new { error = "TargetUrl must use http or https and resolve to a public address." });

        try
        {
            var result = await devTunnelService.ReplayAsync(
                new ReplayWebhookRequest(
                    tunnelId,
                    eventId,
                    targetUri,
                    request.MethodOverride,
                    request.HeadersOverride,
                    request.BodyTextOverride,
                    request.BodyBase64Override,
                    request.ContentTypeOverride,
                    allowedAddresses),
                cancellationToken).ConfigureAwait(false);

            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("{tunnelId}/stream")]
    public async Task<IActionResult> Stream(string tunnelId, CancellationToken cancellationToken)
    {
        var tunnel = await devTunnelService.GetAsync(tunnelId, cancellationToken).ConfigureAwait(false);
        if (tunnel is null)
            return NotFound();

        Response.Headers.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        try
        {
            await foreach (var webhookEvent in devTunnelService.StreamEventsAsync(tunnelId, cancellationToken).ConfigureAwait(false))
            {
                var json = JsonSerializer.Serialize(webhookEvent, JsonOptions);
                var payload = $"event: webhook_event\ndata: {json}\n\n";
                await Response.Body.WriteAsync(Encoding.UTF8.GetBytes(payload), cancellationToken).ConfigureAwait(false);
                await Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }

        return new EmptyResult();
    }

    [HttpGet("login/stream")]
    public async Task<IActionResult> LoginStream(CancellationToken cancellationToken)
    {
        try
        {
            Response.Headers.ContentType = "text/event-stream";
            Response.Headers.CacheControl = "no-cache";
            Response.Headers.Connection = "keep-alive";

            await foreach (var line in devTunnelService.LoginWithDeviceCodeAsync(cancellationToken).ConfigureAwait(false))
            {
                var json = JsonSerializer.Serialize(new { line }, JsonOptions);
                var payload = $"event: login_line\ndata: {json}\n\n";
                await Response.Body.WriteAsync(Encoding.UTF8.GetBytes(payload), cancellationToken).ConfigureAwait(false);
                await Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            const string completePayload = "event: login_complete\ndata: {\"ok\":true}\n\n";
            await Response.Body.WriteAsync(Encoding.UTF8.GetBytes(completePayload), cancellationToken).ConfigureAwait(false);
            await Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (DevTunnelCliUnavailableException ex) when (!Response.HasStarted)
        {
            return DevTunnelCliUnavailable(ex);
        }

        return new EmptyResult();
    }

    private ObjectResult DevTunnelCliUnavailable(DevTunnelCliUnavailableException ex)
        => StatusCode(StatusCodes.Status503ServiceUnavailable, new
        {
            error = "Dev Tunnel CLI unavailable.",
            detail = ex.Message
        });

    private static async Task<IReadOnlyList<IPAddress>?> ResolveReplayAddressesAsync(Uri targetUri, CancellationToken cancellationToken)
    {
        if (!string.Equals(targetUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(targetUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (targetUri.IsLoopback
            || string.Equals(targetUri.Host, "localhost", StringComparison.OrdinalIgnoreCase)
            || targetUri.Host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (IPAddress.TryParse(targetUri.Host, out var ipAddress))
            return IsPrivateAddress(ipAddress) ? null : [ipAddress];

        try
        {
            var resolvedAddresses = await Dns.GetHostAddressesAsync(targetUri.DnsSafeHost, cancellationToken).ConfigureAwait(false);
            return resolvedAddresses.Length > 0 && resolvedAddresses.All(address => !IsPrivateAddress(address))
                ? resolvedAddresses
                : null;
        }
        catch (SocketException)
        {
            return null;
        }
    }

    private static bool IsPrivateAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address))
            return true;

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return bytes[0] switch
            {
                0 => true,
                10 => true,
                127 => true,
                169 when bytes[1] == 254 => true,
                172 when bytes[1] >= 16 && bytes[1] <= 31 => true,
                192 when bytes[1] == 168 => true,
                _ => false
            };
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal)
                return true;

            var bytes = address.GetAddressBytes();
            return (bytes[0] & 0xFE) == 0xFC;
        }

        return false;
    }
}
