using System.Text.Json;
using System.Text.Json.Serialization;

namespace Garrard.Mcp.Explorer.Core.Domain.Workflows;

/// <summary>
/// Handles backward-compatible deserialization of WorkflowStepResult.
/// Old format: { "success": bool, ... }
/// New format: { "status": "Completed"|"Failed"|..., ... }
/// </summary>
public sealed class WorkflowStepResultJsonConverter : JsonConverter<WorkflowStepResult>
{
    public override WorkflowStepResult Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        var stepNumber = root.TryGetProperty("stepNumber", out var sn) ? sn.GetInt32() : 0;
        var toolName = root.TryGetProperty("toolName", out var tn) ? tn.GetString() ?? string.Empty : string.Empty;
        var errorMessage = root.TryGetProperty("errorMessage", out var em) && em.ValueKind != JsonValueKind.Null ? em.GetString() : null;
        var inputJson = root.TryGetProperty("inputJson", out var ij) && ij.ValueKind != JsonValueKind.Null ? ij.GetString() : null;
        var outputJson = root.TryGetProperty("outputJson", out var oj) && oj.ValueKind != JsonValueKind.Null ? oj.GetString() : null;

        DateTime? startedUtc = root.TryGetProperty("startedUtc", out var su) && su.ValueKind != JsonValueKind.Null
            ? su.GetDateTime() : null;
        DateTime? completedUtc = root.TryGetProperty("completedUtc", out var cu) && cu.ValueKind != JsonValueKind.Null
            ? cu.GetDateTime() : null;

        // Support both new `status` string and old `success` boolean
        StepExecutionStatus status;
        if (root.TryGetProperty("status", out var st) && st.ValueKind == JsonValueKind.String
            && Enum.TryParse<StepExecutionStatus>(st.GetString(), out var parsed))
        {
            status = parsed;
        }
        else if (root.TryGetProperty("success", out var sc))
        {
            status = sc.ValueKind == JsonValueKind.True
                ? StepExecutionStatus.Completed
                : StepExecutionStatus.Failed;
        }
        else
        {
            status = StepExecutionStatus.Pending;
        }

        return new WorkflowStepResult
        {
            StepNumber = stepNumber,
            ToolName = toolName,
            Status = status,
            StartedUtc = startedUtc,
            CompletedUtc = completedUtc,
            InputJson = inputJson,
            OutputJson = outputJson,
            ErrorMessage = errorMessage
        };
    }

    public override void Write(Utf8JsonWriter writer, WorkflowStepResult value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("stepNumber", value.StepNumber);
        writer.WriteString("toolName", value.ToolName);
        writer.WriteString("status", value.Status.ToString());
        if (value.StartedUtc.HasValue)
            writer.WriteString("startedUtc", value.StartedUtc.Value);
        if (value.CompletedUtc.HasValue)
            writer.WriteString("completedUtc", value.CompletedUtc.Value);
        if (value.InputJson is not null)
            writer.WriteString("inputJson", value.InputJson);
        if (value.OutputJson is not null)
            writer.WriteString("outputJson", value.OutputJson);
        if (value.ErrorMessage is not null)
            writer.WriteString("errorMessage", value.ErrorMessage);
        writer.WriteEndObject();
    }
}
