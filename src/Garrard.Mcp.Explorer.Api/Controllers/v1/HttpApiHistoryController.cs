using Asp.Versioning;
using Garrard.Mcp.Explorer.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Garrard.Mcp.Explorer.Api.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/http-api-history")]
public sealed class HttpApiHistoryController(IHttpApiSnapshotStore snapshotStore) : ControllerBase
{
    /// <summary>Global invocation history across all endpoints, newest first.</summary>
    [HttpGet]
    public async Task<IActionResult> GetGlobal([FromQuery] int? limit, CancellationToken ct)
    {
        var history = await snapshotStore.GetGlobalHistoryAsync(limit, ct);
        return Ok(history);
    }
}
