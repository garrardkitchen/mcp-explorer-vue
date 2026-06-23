using System.Text.Json;
using System.Text.Json.Nodes;

namespace Garrard.Mcp.Explorer.Cli.Runbooks;

public static class RunbookAssertionEvaluator
{
    public static (bool IsSuccess, string? Message) Evaluate(object? stepResult, RunbookAssertion assertion)
    {
        ArgumentNullException.ThrowIfNull(assertion);

        if (!TryResolvePath(stepResult, assertion.Path, out var actual, out var pathError))
            return (false, pathError);

        var op = (assertion.Operator ?? "equals").Trim().ToLowerInvariant();
        return op switch
        {
            "equals" => EvaluateEquals(assertion.Path, actual, assertion.Value),
            "notequals" => EvaluateNotEquals(assertion.Path, actual, assertion.Value),
            "contains" => EvaluateContains(assertion.Path, actual, assertion.Value),
            "exists" => (true, null),
            _ => (false, $"Unsupported assertion operator '{assertion.Operator}'.")
        };
    }

    private static (bool IsSuccess, string? Message) EvaluateEquals(string? path, object? actual, object? expected)
    {
        if (DeepEquals(actual, expected))
            return (true, null);

        return (false, $"Assertion failed at '{NormalizePath(path)}': expected equals {FormatValue(expected)} but got {FormatValue(actual)}.");
    }

    private static (bool IsSuccess, string? Message) EvaluateNotEquals(string? path, object? actual, object? expected)
    {
        if (!DeepEquals(actual, expected))
            return (true, null);

        return (false, $"Assertion failed at '{NormalizePath(path)}': expected value to differ from {FormatValue(expected)}.");
    }

    private static (bool IsSuccess, string? Message) EvaluateContains(string? path, object? actual, object? expected)
    {
        var expectedText = expected?.ToString() ?? string.Empty;
        var actualText = actual?.ToString() ?? string.Empty;

        if (actualText.Contains(expectedText, StringComparison.Ordinal))
            return (true, null);

        return (false, $"Assertion failed at '{NormalizePath(path)}': expected '{actualText}' to contain '{expectedText}'.");
    }

    private static bool TryResolvePath(object? value, string? path, out object? resolved, out string? error)
    {
        resolved = value;
        error = null;

        if (string.IsNullOrWhiteSpace(path) || path == "$")
            return true;

        var node = value switch
        {
            JsonNode jsonNode => jsonNode,
            null => null,
            _ => JsonSerializer.SerializeToNode(value)
        };

        if (node is null)
        {
            error = $"Assertion path '{path}' could not be resolved because the step result is null.";
            return false;
        }

        foreach (var part in path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (node is JsonObject obj)
            {
                if (!obj.TryGetPropertyValue(part, out node))
                {
                    error = $"Assertion path '{path}' is invalid at segment '{part}'.";
                    return false;
                }

                continue;
            }

            if (node is JsonArray arr && int.TryParse(part, out var index) && index >= 0 && index < arr.Count)
            {
                node = arr[index];
                continue;
            }

            error = $"Assertion path '{path}' is invalid at segment '{part}'.";
            return false;
        }

        resolved = node is JsonValue jsonValue
            ? jsonValue.GetValue<object?>()
            : node;

        return true;
    }

    private static bool DeepEquals(object? left, object? right)
    {
        if (left is JsonElement le) left = NormalizeJsonElement(le);
        if (right is JsonElement re) right = NormalizeJsonElement(re);

        var leftJson = JsonSerializer.Serialize(left);
        var rightJson = JsonSerializer.Serialize(right);
        return string.Equals(leftJson, rightJson, StringComparison.Ordinal);
    }

    private static object? NormalizeJsonElement(JsonElement element)
        => element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => JsonSerializer.Deserialize<object?>(element.GetRawText())
        };

    private static string NormalizePath(string? path) => string.IsNullOrWhiteSpace(path) ? "$" : path;

    private static string FormatValue(object? value)
        => value is null ? "null" : JsonSerializer.Serialize(value);
}
