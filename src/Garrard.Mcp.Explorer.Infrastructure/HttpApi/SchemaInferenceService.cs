using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Garrard.Mcp.Explorer.Core.Interfaces;

namespace Garrard.Mcp.Explorer.Infrastructure.HttpApi;

/// <summary>
/// Walks a JSON response body recursively and produces a lightweight schema
/// (type + properties for objects, items for arrays — no actual values).
/// </summary>
public sealed class SchemaInferenceService : ISchemaInferenceService
{
    public Dictionary<string, object?> InferSchema(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new Dictionary<string, object?> { ["type"] = "null" };

        try
        {
            var node = JsonNode.Parse(json);
            return BuildSchema(node);
        }
        catch
        {
            return new Dictionary<string, object?> { ["type"] = "string" };
        }
    }

    public string ComputeSchemaHash(Dictionary<string, object?> schema)
    {
        var canonical = JsonSerializer.Serialize(schema, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static Dictionary<string, object?> BuildSchema(JsonNode? node)
    {
        return node switch
        {
            null                => new Dictionary<string, object?> { ["type"] = "null" },
            JsonObject obj      => BuildObjectSchema(obj),
            JsonArray arr       => BuildArraySchema(arr),
            JsonValue val       => BuildValueSchema(val),
            _                   => new Dictionary<string, object?> { ["type"] = "unknown" }
        };
    }

    private static Dictionary<string, object?> BuildObjectSchema(JsonObject obj)
    {
        var props = new Dictionary<string, object?>();
        foreach (var (key, val) in obj)
            props[key] = BuildSchema(val);

        return new Dictionary<string, object?>
        {
            ["type"] = "object",
            ["properties"] = props
        };
    }

    private static Dictionary<string, object?> BuildArraySchema(JsonArray arr)
    {
        // Infer items schema from first element
        var itemSchema = arr.Count > 0
            ? BuildSchema(arr[0])
            : new Dictionary<string, object?> { ["type"] = "unknown" };

        return new Dictionary<string, object?>
        {
            ["type"] = "array",
            ["items"] = itemSchema
        };
    }

    private static Dictionary<string, object?> BuildValueSchema(JsonValue val)
    {
        var raw = val.ToJsonString();

        // Determine type from the raw JSON token
        if (raw == "null")                       return new Dictionary<string, object?> { ["type"] = "null" };
        if (raw == "true" || raw == "false")     return new Dictionary<string, object?> { ["type"] = "boolean" };
        if (raw.StartsWith('"'))                 return new Dictionary<string, object?> { ["type"] = "string" };
        if (raw.Contains('.'))                   return new Dictionary<string, object?> { ["type"] = "number" };
        if (long.TryParse(raw, out _))           return new Dictionary<string, object?> { ["type"] = "integer" };

        return new Dictionary<string, object?> { ["type"] = "number" };
    }
}
