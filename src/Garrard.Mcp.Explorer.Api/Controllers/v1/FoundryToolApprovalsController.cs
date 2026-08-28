using Asp.Versioning;
using Garrard.Mcp.Explorer.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Garrard.Mcp.Explorer.Api.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/chat/approvals")]
public sealed class FoundryToolApprovalsController(IFoundryToolApprovalService approvals) : ControllerBase
{
    [HttpPost("{approvalRequestId}")]
    public IActionResult Resolve(string approvalRequestId, [FromBody] ResolveFoundryToolApprovalRequest request)
    {
        return approvals.TryResolve(approvalRequestId, request.Approved)
            ? NoContent()
            : NotFound(new { error = "The approval request is no longer pending." });
    }
}

public sealed record ResolveFoundryToolApprovalRequest(bool Approved);
