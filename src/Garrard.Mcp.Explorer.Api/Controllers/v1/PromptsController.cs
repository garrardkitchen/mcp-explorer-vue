using Asp.Versioning;
using Garrard.Mcp.Explorer.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Garrard.Mcp.Explorer.Api.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/connections/{connectionName}/prompts")]
public sealed class PromptsController(IConnectionService connectionService) : ControllerBase
{
    [HttpGet]
    public IActionResult GetPrompts(string connectionName)
    {
        var conn = connectionService.GetConnection(connectionName);
        if (conn is null) return NotFound($"Connection '{connectionName}' is not active.");
        return Ok(conn.Prompts);
    }

    [HttpPost("{promptName}/execute")]
    public async Task<IActionResult> Execute(
        string connectionName,
        string promptName,
        [FromBody] Dictionary<string, string> arguments,
        CancellationToken cancellationToken)
    {
        var result = await connectionService.ExecutePromptAsync(connectionName, promptName, arguments, cancellationToken);
        return Ok(new { result });
    }
}
