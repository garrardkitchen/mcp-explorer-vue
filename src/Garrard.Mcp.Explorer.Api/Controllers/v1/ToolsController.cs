using Asp.Versioning;
using Garrard.Mcp.Explorer.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Garrard.Mcp.Explorer.Api.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/connections/{connectionName}/tools")]
public sealed class ToolsController(
    IConnectionService connectionService,
    IConfiguration configuration) : ControllerBase
{
    private TimeSpan InvokeTimeout
    {
        get
        {
            var seconds = configuration.GetValue<int>("ToolInvoke:TimeoutSeconds", 300);
            return TimeSpan.FromSeconds(seconds > 0 ? seconds : 300);
        }
    }

    [HttpGet]
    public IActionResult GetTools(string connectionName)
    {
        var conn = connectionService.GetConnection(connectionName);
        if (conn is null) return NotFound($"Connection '{connectionName}' is not active.");
        return Ok(conn.Tools);
    }

    [HttpPost("{toolName}/invoke")]
    public async Task<IActionResult> Invoke(
        string connectionName,
        string toolName,
        [FromBody] Dictionary<string, object?> parameters,
        CancellationToken requestAborted)
    {
        // Use an independent timeout rather than the request-abort token.
        // The request token fires when nginx drops the client connection (default
        // 60 s proxy_read_timeout), which would cancel an in-flight MCP call that
        // is simply taking longer than the proxy timeout.
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(requestAborted);
        cts.CancelAfter(InvokeTimeout);

        try
        {
            var result = await connectionService.InvokeToolAsync(
                connectionName, toolName, parameters, cts.Token);
            return Ok(new { result });
        }
        catch (OperationCanceledException) when (requestAborted.IsCancellationRequested)
        {
            // Browser/client disconnected — not an error on our end
            return StatusCode(499, new { error = "Client closed the request." });
        }
        catch (OperationCanceledException)
        {
            // Our own timeout fired
            return StatusCode(504, new
            {
                error = $"Tool invocation timed out after {InvokeTimeout.TotalSeconds:0} s.",
                tool = toolName
            });
        }
        catch (Exception ex)
        {
            // Let the global middleware handle unexpected errors, but surface the
            // MCP error message if it's available
            return StatusCode(502, new { error = ex.Message, tool = toolName });
        }
    }
}
