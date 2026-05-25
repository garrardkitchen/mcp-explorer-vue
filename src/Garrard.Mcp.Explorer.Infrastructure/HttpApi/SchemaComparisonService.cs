using System.Text.Json;
using Garrard.Mcp.Explorer.Core.Domain.HttpApi;
using Garrard.Mcp.Explorer.Core.Interfaces;

namespace Garrard.Mcp.Explorer.Infrastructure.HttpApi;

/// <summary>
/// Compares a live response schema against a saved snapshot baseline and
/// classifies each difference as breaking, non-breaking, or a performance degradation.
/// </summary>
public sealed class SchemaComparisonService : ISchemaComparisonService
{
    public HttpSchemaComparisonResult Compare(
        HttpApiDefinition endpoint,
        HttpResponseSnapshot baseline,
        int liveStatusCode,
        long liveLatencyMs,
        Dictionary<string, object?> liveSchema,
        double degradationThresholdMultiplier = 2.0)
    {
        var result = new HttpSchemaComparisonResult
        {
            EndpointId          = endpoint.Id,
            EndpointName        = endpoint.Name,
            SnapshotId          = baseline.Id,
            LiveStatusCode      = liveStatusCode,
            SnapshotStatusCode  = baseline.StatusCode,
            LiveLatencyMs       = liveLatencyMs,
            SnapshotLatencyMs   = baseline.LatencyMs,
            IsDegraded          = baseline.LatencyMs > 0 &&
                                  liveLatencyMs > baseline.LatencyMs * degradationThresholdMultiplier
        };

        // Flatten both schemas to leaf paths ("a.b.c" → type string)
        var livePaths     = new Dictionary<string, string>(StringComparer.Ordinal);
        var baselinePaths = new Dictionary<string, string>(StringComparer.Ordinal);

        FlattenSchema(liveSchema, string.Empty, livePaths);
        FlattenSchema(baseline.InferredSchema, string.Empty, baselinePaths);

        foreach (var (path, type) in livePaths)
        {
            if (!baselinePaths.TryGetValue(path, out var baselineType))
                result.AddedProperties.Add(path);
            else if (!string.Equals(type, baselineType, StringComparison.OrdinalIgnoreCase))
                result.ChangedTypes.Add(new SchemaPropertyChange
                {
                    PropertyPath = path,
                    PreviousType = baselineType,
                    CurrentType  = type
                });
        }

        foreach (var path in baselinePaths.Keys)
            if (!livePaths.ContainsKey(path))
                result.RemovedProperties.Add(path);

        return result;
    }

    private static void FlattenSchema(
        Dictionary<string, object?> schema,
        string prefix,
        Dictionary<string, string> result)
    {
        if (!schema.TryGetValue("type", out var typeVal)) return;
        var type = typeVal?.ToString() ?? "unknown";

        switch (type)
        {
            case "object":
            {
                if (!schema.TryGetValue("properties", out var propsVal)) break;
                Dictionary<string, object?>? props = null;

                if (propsVal is Dictionary<string, object?> d)
                    props = d;
                else if (propsVal is JsonElement je && je.ValueKind == JsonValueKind.Object)
                    props = je.Deserialize<Dictionary<string, object?>>();

                if (props is null) break;

                foreach (var (key, val) in props)
                {
                    var childPath = string.IsNullOrEmpty(prefix) ? key : $"{prefix}.{key}";
                    if (val is Dictionary<string, object?> childSchema)
                        FlattenSchema(childSchema, childPath, result);
                    else if (val is JsonElement elem && elem.ValueKind == JsonValueKind.Object)
                    {
                        var childDict = elem.Deserialize<Dictionary<string, object?>>();
                        if (childDict is not null) FlattenSchema(childDict, childPath, result);
                    }
                }
                break;
            }
            case "array":
            {
                var itemPath = string.IsNullOrEmpty(prefix) ? "[]" : $"{prefix}[]";
                if (schema.TryGetValue("items", out var itemsVal))
                {
                    if (itemsVal is Dictionary<string, object?> itemSchema)
                        FlattenSchema(itemSchema, itemPath, result);
                    else if (itemsVal is JsonElement je && je.ValueKind == JsonValueKind.Object)
                    {
                        var itemDict = je.Deserialize<Dictionary<string, object?>>();
                        if (itemDict is not null) FlattenSchema(itemDict, itemPath, result);
                    }
                }
                break;
            }
            default:
                // Leaf node — record the type
                result[string.IsNullOrEmpty(prefix) ? "$root" : prefix] = type;
                break;
        }
    }
}
