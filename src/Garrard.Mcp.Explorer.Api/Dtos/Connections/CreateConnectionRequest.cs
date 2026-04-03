using Garrard.Mcp.Explorer.Core.Domain.Connections;

namespace Garrard.Mcp.Explorer.Api.Dtos.Connections;

public sealed record CreateConnectionRequest(
    string Name,
    string Endpoint,
    string? Note,
    string? GroupName,
    ConnectionAuthenticationMode AuthenticationMode = ConnectionAuthenticationMode.CustomHeaders,
    List<ConnectionHeader>? Headers = null,
    AzureClientCredentialsOptions? AzureCredentials = null,
    OAuthConnectionOptions? OAuthOptions = null);
