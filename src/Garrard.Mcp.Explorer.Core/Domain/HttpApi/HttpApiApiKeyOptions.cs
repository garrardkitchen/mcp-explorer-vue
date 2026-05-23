namespace Garrard.Mcp.Explorer.Core.Domain.HttpApi;

public sealed class HttpApiApiKeyOptions
{
    /// <summary>Header name to send the API key in (e.g. "X-Api-Key" or "Authorization").</summary>
    public string HeaderName { get; set; } = "X-Api-Key";
    public string ApiKey { get; set; } = string.Empty;
    /// <summary>Optional prefix added before the key value (e.g. "Bearer " or "ApiKey ").</summary>
    public string? Prefix { get; set; }
}
