namespace Garrard.Mcp.Explorer.Core.Interfaces;

/// <summary>
/// Infers a lightweight JSON schema (shape only, no values) from a deserialized JSON element.
/// </summary>
public interface ISchemaInferenceService
{
    /// <summary>
    /// Walks the JSON payload and returns a dictionary representing the schema
    /// (e.g. {"type":"object","properties":{"id":{"type":"number"},...}}).
    /// </summary>
    Dictionary<string, object?> InferSchema(string? json);

    /// <summary>Computes a stable SHA-256 hash of the inferred schema for drift detection.</summary>
    string ComputeSchemaHash(Dictionary<string, object?> schema);
}
