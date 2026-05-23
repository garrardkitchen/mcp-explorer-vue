namespace Garrard.Mcp.Explorer.Core.Domain.HttpApi;

public sealed class HttpApiQueryParam
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
}
