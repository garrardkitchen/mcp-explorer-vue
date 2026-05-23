using Garrard.Mcp.Explorer.Core.Domain.HttpApi;

namespace Garrard.Mcp.Explorer.Core.Interfaces;

/// <summary>
/// Compares two inferred schemas and produces a structured diff.
/// </summary>
public interface ISchemaComparisonService
{
    HttpSchemaComparisonResult Compare(
        HttpApiDefinition endpoint,
        HttpResponseSnapshot baseline,
        int liveStatusCode,
        long liveLatencyMs,
        Dictionary<string, object?> liveSchema,
        double degradationThresholdMultiplier = 2.0);
}
