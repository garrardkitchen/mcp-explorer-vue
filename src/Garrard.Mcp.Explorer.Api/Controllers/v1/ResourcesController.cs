using Asp.Versioning;
using Garrard.Mcp.Explorer.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Garrard.Mcp.Explorer.Api.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/connections/{connectionName}")]
public sealed class ResourcesController(IConnectionService connectionService) : ControllerBase
{
    [HttpGet("resources")]
    public IActionResult GetResources(string connectionName)
    {
        var conn = connectionService.GetConnection(connectionName);
        if (conn is null) return NotFound();
        return Ok(conn.Resources);
    }

    [HttpGet("resources/{*uri}")]
    public async Task<IActionResult> ReadResource(string connectionName, string uri, CancellationToken cancellationToken)
    {
        var result = await connectionService.ReadResourceAsync(connectionName, Uri.UnescapeDataString(uri), cancellationToken);
        return Ok(new { result });
    }

    [HttpGet("resource-templates")]
    public IActionResult GetResourceTemplates(string connectionName)
    {
        var conn = connectionService.GetConnection(connectionName);
        if (conn is null) return NotFound();
        return Ok(conn.ResourceTemplates);
    }
}
