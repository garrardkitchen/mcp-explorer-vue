namespace Garrard.Mcp.Explorer.Core.Domain.HttpApi;

public enum HttpApiAuthenticationMode
{
    None = 0,
    ApiKey = 1,
    Bearer = 2,
    AzureClientCredentials = 3,
    CustomHeaders = 4
}
