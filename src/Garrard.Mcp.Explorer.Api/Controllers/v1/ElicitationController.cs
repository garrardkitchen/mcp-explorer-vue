using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Asp.Versioning;
using Garrard.Mcp.Explorer.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Garrard.Mcp.Explorer.Api.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/elicitation")]
public sealed class ElicitationController(IElicitationService elicitationService) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [HttpGet("pending")]
    public IActionResult GetPending([FromQuery] string? connectionName)
        => Ok(elicitationService.GetPendingRequests(connectionName));

    [HttpPost("{requestId}/respond")]
    public async Task<IActionResult> Respond(
        string requestId,
        [FromBody] SubmitElicitationRequest request,
        CancellationToken ct)
    {
        var success = await elicitationService.SubmitResponseAsync(requestId, request.Action, request.Content, ct);
        return success ? Ok() : NotFound();
    }

    [HttpGet("history")]
    public IActionResult GetHistory([FromQuery] string? connectionName)
        => Ok(elicitationService.GetHistory(connectionName));

    [HttpGet("stream")]
    public async Task Stream(CancellationToken cancellationToken)
    {
        Response.Headers.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        var channel = Channel.CreateUnbounded<ElicitationRequestedEventArgs>();

        void OnElicitation(object? _, ElicitationRequestedEventArgs args)
            => channel.Writer.TryWrite(args);

        elicitationService.ElicitationRequested += OnElicitation;
        try
        {
            await foreach (var args in channel.Reader.ReadAllAsync(cancellationToken))
            {
                var json = JsonSerializer.Serialize(args.Request, JsonOptions);
                var payload = $"event: elicitation_request\ndata: {json}\n\n";
                await Response.Body.WriteAsync(Encoding.UTF8.GetBytes(payload), cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            elicitationService.ElicitationRequested -= OnElicitation;
            channel.Writer.TryComplete();
        }
    }
}

public sealed record SubmitElicitationRequest(string Action, Dictionary<string, JsonElement>? Content);
