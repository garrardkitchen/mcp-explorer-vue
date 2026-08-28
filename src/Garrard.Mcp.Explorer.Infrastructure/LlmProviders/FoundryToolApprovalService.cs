using System.Collections.Concurrent;
using Garrard.Mcp.Explorer.Core.Interfaces;

namespace Garrard.Mcp.Explorer.Infrastructure.LlmProviders;

public sealed class FoundryToolApprovalService : IFoundryToolApprovalService
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource<FoundryToolApprovalDecision>> _pending =
        new(StringComparer.Ordinal);

    public Task<FoundryToolApprovalDecision> WaitForDecisionAsync(
        string approvalRequestId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(approvalRequestId);

        var completion = new TaskCompletionSource<FoundryToolApprovalDecision>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pending.TryAdd(approvalRequestId, completion))
            throw new InvalidOperationException($"Approval request '{approvalRequestId}' is already pending.");

        return AwaitDecisionAsync(approvalRequestId, completion, cancellationToken);
    }

    public bool TryResolve(string approvalRequestId, bool approved)
    {
        if (string.IsNullOrWhiteSpace(approvalRequestId))
            return false;

        return _pending.TryGetValue(approvalRequestId, out var completion) &&
               completion.TrySetResult(new FoundryToolApprovalDecision(approved));
    }

    private async Task<FoundryToolApprovalDecision> AwaitDecisionAsync(
        string approvalRequestId,
        TaskCompletionSource<FoundryToolApprovalDecision> completion,
        CancellationToken cancellationToken)
    {
        try
        {
            return await completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _pending.TryRemove(new KeyValuePair<string, TaskCompletionSource<FoundryToolApprovalDecision>>(
                approvalRequestId,
                completion));
        }
    }
}
