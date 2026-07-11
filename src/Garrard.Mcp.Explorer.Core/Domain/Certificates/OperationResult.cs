namespace Garrard.Mcp.Explorer.Core.Domain.Certificates;

public enum StepStatus
{
    Succeeded = 0,
    Failed = 1,
    Skipped = 2,
}

/// <summary>One step of a multi-step certificate operation (generate, upload, renew, test-token).</summary>
public sealed record StepResult(string Id, string Label, StepStatus Status, string? Message = null);

/// <summary>
/// Result of a multi-step certificate operation. The frontend animates <see cref="Steps"/>
/// in order; a failed operation reports the failing step with a remediation message.
/// </summary>
public sealed record OperationResult
{
    public bool Success { get; init; }
    public IReadOnlyList<StepResult> Steps { get; init; } = [];
    public CertificateInfo? Certificate { get; init; }

    public static OperationResult Succeeded(IReadOnlyList<StepResult> steps, CertificateInfo? certificate = null)
        => new() { Success = true, Steps = steps, Certificate = certificate };

    public static OperationResult Failed(IReadOnlyList<StepResult> steps, CertificateInfo? certificate = null)
        => new() { Success = false, Steps = steps, Certificate = certificate };
}
