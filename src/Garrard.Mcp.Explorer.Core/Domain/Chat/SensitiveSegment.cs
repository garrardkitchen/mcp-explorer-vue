namespace Garrard.Mcp.Explorer.Core.Domain.Chat;

public sealed class SensitiveSegment
{
    public int Start { get; init; }
    public int Length { get; init; }
    public string EncryptedValue { get; init; } = string.Empty;
    public bool IsRevealed { get; set; }
}
