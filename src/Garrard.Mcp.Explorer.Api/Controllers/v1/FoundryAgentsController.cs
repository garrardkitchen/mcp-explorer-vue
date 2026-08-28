using Asp.Versioning;
using Garrard.Mcp.Explorer.Core.Domain.LlmModels;
using Garrard.Mcp.Explorer.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Garrard.Mcp.Explorer.Api.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/llm-models/foundry-project")]
public sealed class FoundryAgentsController(IFoundryProjectAgentService foundryAgents) : ControllerBase
{
    [HttpPost("agents/discover")]
    public async Task<IActionResult> Discover(
        [FromBody] LlmModelDefinition model,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await foundryAgents.DiscoverAgentsAsync(model, cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
