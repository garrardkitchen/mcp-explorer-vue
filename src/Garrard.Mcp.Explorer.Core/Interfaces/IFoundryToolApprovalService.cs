namespace Garrard.Mcp.Explorer.Core.Interfaces;

public interface IFoundryToolApprovalService
{
    Task<FoundryToolApprovalDecision> WaitForDecisionAsync(
        string approvalRequestId,
        CancellationToken cancellationToken = default);

    bool TryResolve(string approvalRequestId, bool approved);
}

public sealed record FoundryToolApprovalDecision(bool Approved);
