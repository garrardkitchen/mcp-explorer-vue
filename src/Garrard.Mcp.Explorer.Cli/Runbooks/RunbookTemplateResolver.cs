using System.Text.RegularExpressions;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Garrard.Mcp.Explorer.Cli.Runbooks;

public sealed class RunbookTemplateResolver
{
    private static readonly Regex TemplatePattern = new("\\{\\{\\s*([^}]+)\\s*}}", RegexOptions.Compiled);

    public object? ResolveObject(object? value, IReadOnlyDictionary<string, object?> stepResults, int iterationIndex)
    {
        return value switch
        {
            null => null,
            string s => ResolveString(s, stepResults, iterationIndex),
            IDictionary<object, object?> dict => dict.ToDictionary(kvp => kvp.Key.ToString() ?? string.Empty,
                kvp => ResolveObject(kvp.Value, stepResults, iterationIndex), StringComparer.OrdinalIgnoreCase),
            IDictionary<string, object?> dictString => dictString.ToDictionary(kvp => kvp.Key,
                kvp => ResolveObject(kvp.Value, stepResults, iterationIndex), StringComparer.OrdinalIgnoreCase),
            IEnumerable<object?> list => list.Select(x => ResolveObject(x, stepResults, iterationIndex)).ToList(),
            _ => value
        };
    }

    private object? ResolveString(string template, IReadOnlyDictionary<string, object?> stepResults, int iterationIndex)
    {
        var matches = TemplatePattern.Matches(template);
        if (matches.Count == 0) return template;

        if (matches.Count == 1 && matches[0].Value.Length == template.Length)
        {
            return ResolveToken(matches[0].Groups[1].Value.Trim(), stepResults, iterationIndex);
        }

        // Single pass so resolved values that themselves contain '{{...}}' text are never re-expanded.
        return TemplatePattern.Replace(template, match =>
        {
            var token = match.Groups[1].Value.Trim();
            var value = ResolveToken(token, stepResults, iterationIndex);
            return value?.ToString() ?? string.Empty;
        });
    }

    private static object? ResolveToken(string token, IReadOnlyDictionary<string, object?> stepResults, int iterationIndex)
    {
        if (token.Equals("iteration.index", StringComparison.OrdinalIgnoreCase))
            return iterationIndex;

        if (token.StartsWith("env.", StringComparison.OrdinalIgnoreCase))
            return Environment.GetEnvironmentVariable(token[4..]) ?? string.Empty;

        if (!token.StartsWith("steps.", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Unsupported runbook template token '{token}'.");

        var path = token[6..];
        var firstDot = path.IndexOf('.');
        var stepId = firstDot >= 0 ? path[..firstDot] : path;

        if (!stepResults.TryGetValue(stepId, out var stepValue))
            throw new InvalidOperationException($"Template token '{token}' references unknown step '{stepId}'.");

        if (firstDot < 0)
            return stepValue;

        var propertyPath = path[(firstDot + 1)..];
        return ResolvePropertyPath(stepValue, propertyPath);
    }

    private static object? ResolvePropertyPath(object? value, string propertyPath)
    {
        if (value is null) return null;

        var node = value switch
        {
            JsonNode jsonNode => jsonNode,
            _ => JsonSerializer.SerializeToNode(value)
        };

        foreach (var part in propertyPath.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (node is JsonObject obj)
            {
                if (!obj.TryGetPropertyValue(part, out node))
                    throw new InvalidOperationException($"Property '{part}' not found in path '{propertyPath}'.");
                continue;
            }

            if (node is JsonArray arr && int.TryParse(part, out var index) && index >= 0 && index < arr.Count)
            {
                node = arr[index];
                continue;
            }

            throw new InvalidOperationException($"Path '{propertyPath}' is invalid at segment '{part}'.");
        }

        if (node is JsonValue jsonValue)
            return jsonValue.GetValue<object?>();

        return node;
    }
}
