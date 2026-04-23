using Asp.Versioning;
using Garrard.Mcp.Explorer.Core.Domain.DevTunnels;
using Garrard.Mcp.Explorer.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace Garrard.Mcp.Explorer.Api.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[AllowAnonymous]
[Route("api/v{version:apiVersion}/webhooks")]
public sealed class WebhookCaptureController(IDevTunnelService devTunnelService, IConfiguration configuration) : ControllerBase
{
    private readonly int _maxCaptureBytes = Math.Max(1, configuration.GetValue("DevTunnels:MaxCaptureBytes", 1024 * 1024));

    [Route("{tunnelId}")]
    [Route("{tunnelId}/{**path}")]
    [AcceptVerbs("GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS")]
    [RequestSizeLimit(2 * 1024 * 1024)]
    public async Task<IActionResult> Capture(string tunnelId, string? path, CancellationToken cancellationToken)
    {
        try
        {
            if (Request.ContentLength is > 0 && Request.ContentLength > _maxCaptureBytes)
            {
                return StatusCode(StatusCodes.Status413PayloadTooLarge, new
                {
                    error = $"Payload exceeds the configured capture limit of {_maxCaptureBytes} bytes."
                });
            }

            var body = await ReadRequestBodyAsync(cancellationToken).ConfigureAwait(false);
            if (body.Length > _maxCaptureBytes)
            {
                return StatusCode(StatusCodes.Status413PayloadTooLarge, new
                {
                    error = $"Payload exceeds the configured capture limit of {_maxCaptureBytes} bytes."
                });
            }

            var headers = Request.Headers.ToDictionary(
                h => h.Key,
                h => string.Join(", ", h.Value.ToArray()),
                StringComparer.OrdinalIgnoreCase);

            await devTunnelService.CaptureAsync(
                new WebhookCaptureRequest(
                    TunnelId: tunnelId,
                    Method: Request.Method,
                    Path: "/" + (path ?? string.Empty).TrimStart('/'),
                    QueryString: Request.QueryString.Value ?? string.Empty,
                    Headers: headers,
                    ContentType: Request.ContentType,
                    RemoteIp: HttpContext.Connection.RemoteIpAddress?.ToString(),
                    Body: body),
                cancellationToken).ConfigureAwait(false);

            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    private async Task<byte[]> ReadRequestBodyAsync(CancellationToken cancellationToken)
    {
        Request.EnableBuffering();

        using var memory = new MemoryStream();
        var buffer = new byte[81920];
        while (true)
        {
            var remaining = _maxCaptureBytes + 1 - (int)memory.Length;
            if (remaining <= 0)
                break;

            var bytesRead = await Request.Body.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, remaining)), cancellationToken).ConfigureAwait(false);
            if (bytesRead == 0)
                break;

            await memory.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
        }

        Request.Body.Position = 0;
        return memory.ToArray();
    }
}
