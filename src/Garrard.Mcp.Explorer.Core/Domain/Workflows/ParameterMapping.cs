namespace Garrard.Mcp.Explorer.Core.Domain.Workflows;

public sealed class ParameterMapping
{
    public string ParameterName { get; set; } = string.Empty;
    public string? Value { get; set; }
    public int? SourceStepNumber { get; set; }
    public string? SourcePropertyName { get; set; }
}
