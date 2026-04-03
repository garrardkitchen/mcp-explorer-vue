using Asp.Versioning;
using Garrard.Mcp.Explorer.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Garrard.Mcp.Explorer.Api.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/connections/{connectionName}/tools")]
public sealed class ToolsController(IConnectionService connectionService) : ControllerBase
{
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
        CancellationToken cancellationToken)
    {
        var result = await connectionService.InvokeToolAsync(connectionName, toolName, parameters, cancellationToken);
        return Ok(new { result });
    }
}
