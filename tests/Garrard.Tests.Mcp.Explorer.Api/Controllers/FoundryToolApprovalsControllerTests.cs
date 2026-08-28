using Garrard.Mcp.Explorer.Api.Controllers.v1;
using Garrard.Mcp.Explorer.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Garrard.Tests.Mcp.Explorer.Api.Controllers;

public sealed class FoundryToolApprovalsControllerTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Resolve_PendingRequest_ReturnsNoContent(bool approved)
    {
        var approvals = new Mock<IFoundryToolApprovalService>();
        approvals.Setup(a => a.TryResolve("approval-1", approved)).Returns(true);
        var controller = new FoundryToolApprovalsController(approvals.Object);

        var result = controller.Resolve(
            "approval-1",
            new ResolveFoundryToolApprovalRequest(approved));

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public void Resolve_StaleRequest_ReturnsNotFound()
    {
        var approvals = new Mock<IFoundryToolApprovalService>();
        var controller = new FoundryToolApprovalsController(approvals.Object);

        var result = controller.Resolve(
            "missing",
            new ResolveFoundryToolApprovalRequest(true));

        Assert.IsType<NotFoundObjectResult>(result);
    }
}
