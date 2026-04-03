namespace Garrard.Mcp.Explorer.Core.Domain.Workflows;

public sealed class ParameterMapping
{
    /// <summary>The name of the target parameter in the current tool.</summary>
    public string TargetParameter { get; set; } = string.Empty;

    /// <summary>The type of mapping source.</summary>
    public MappingSourceType SourceType { get; set; } = MappingSourceType.ManualValue;

    /// <summary>Which step to extract the value from (null = immediately previous step).</summary>
    public int? SourceStepIndex { get; set; }

    /// <summary>JSONPath into the source step output (e.g. "data[0].id").</summary>
    public string? SourcePropertyPath { get; set; }

    /// <summary>Static / manual value (used when SourceType = ManualValue).</summary>
    public string? ManualValue { get; set; }

    /// <summary>How to handle arrays encountered along the property path.</summary>
    public ArrayIterationMode IterationMode { get; set; } = ArrayIterationMode.None;
}

public enum MappingSourceType
{
    /// <summary>Value extracted from a previous step's output JSON.</summary>
    FromPreviousStep = 0,

    /// <summary>Prompted from the user at execution time (runtime parameter).</summary>
    PromptAtRuntime = 1,

    /// <summary>Static value entered directly in the mapping.</summary>
    ManualValue = 2
}

public enum ArrayIterationMode
{
    /// <summary>No iteration – value extracted as-is.</summary>
    None = 0,

    /// <summary>Iterate every element – the step runs once per element.</summary>
    Each = 1,

    /// <summary>Use only the first element.</summary>
    First = 2,

    /// <summary>Use only the last element.</summary>
    Last = 3
}
