namespace Garrard.Mcp.Explorer.Core.Domain.Preferences;

public sealed record SensitiveFieldConfiguration
{
    public List<string> AdditionalSensitiveFields { get; init; } = [];
    public List<string> AllowedFields { get; init; } = [];
    public bool UseAiDetection { get; init; }
    public AiDetectionStrictness AiStrictness { get; init; } = AiDetectionStrictness.Balanced;
    public bool ShowDetectionDebug { get; init; }
}
