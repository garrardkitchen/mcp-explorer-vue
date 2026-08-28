using Garrard.Mcp.Explorer.Infrastructure.LlmProviders;

namespace Garrard.Tests.Mcp.Explorer.Infrastructure.LlmProviders;

public sealed class FoundryToolApprovalServiceTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WaitForDecisionAsync_Resolved_ReturnsDecisionAndRemovesRequest(bool approved)
    {
        var service = new FoundryToolApprovalService();
        var decisionTask = service.WaitForDecisionAsync("approval-1");

        Assert.True(service.TryResolve("approval-1", approved));
        var decision = await decisionTask;

        Assert.Equal(approved, decision.Approved);
        Assert.False(service.TryResolve("approval-1", approved));
    }

    [Fact]
    public async Task WaitForDecisionAsync_Cancelled_RemovesRequest()
    {
        var service = new FoundryToolApprovalService();
        using var cancellation = new CancellationTokenSource();
        var decisionTask = service.WaitForDecisionAsync("approval-1", cancellation.Token);

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => decisionTask);
        Assert.False(service.TryResolve("approval-1", true));
    }

    [Fact]
    public async Task WaitForDecisionAsync_DuplicatePendingId_Throws()
    {
        var service = new FoundryToolApprovalService();
        using var cancellation = new CancellationTokenSource();
        var first = service.WaitForDecisionAsync("approval-1", cancellation.Token);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await service.WaitForDecisionAsync("approval-1"));

        Assert.Contains("already pending", exception.Message);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
    }
}
