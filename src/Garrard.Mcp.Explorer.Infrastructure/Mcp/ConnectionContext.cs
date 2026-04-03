using Garrard.Mcp.Explorer.Core.Domain.Connections;
using ModelContextProtocol.Client;

namespace Garrard.Mcp.Explorer.Infrastructure.Mcp;

/// <summary>
/// Holds a live MCP client connection along with the capabilities discovered during initialisation.
/// </summary>
internal sealed class ConnectionContext : IAsyncDisposable
{
    internal ConnectionContext(
        string name,
        string endpoint,
        string clientName,
        string clientVersion,
        ConnectionAuthenticationMode authenticationMode,
        AzureClientCredentialsOptions? azureCredentials,
        OAuthConnectionOptions? oAuthOptions,
        IReadOnlyDictionary<string, string> headers,
        HttpClientTransport transport,
        McpClient client,
        IReadOnlyList<McpClientTool> tools,
        IReadOnlyList<McpClientPrompt> prompts,
        IReadOnlyList<McpClientResource> resources,
        IReadOnlyList<McpClientResourceTemplate> resourceTemplates)
    {
        Name = name;
        Endpoint = endpoint;
        ClientName = clientName;
        ClientVersion = clientVersion;
        AuthenticationMode = authenticationMode;
        AzureCredentials = azureCredentials;
        OAuthOptions = oAuthOptions;
        Headers = headers;
        Transport = transport;
        Client = client;
        Tools = tools;
        Prompts = prompts;
        Resources = resources;
        ResourceTemplates = resourceTemplates;
    }

    internal string Name { get; }
    internal string Endpoint { get; }
    internal string ClientName { get; }
    internal string ClientVersion { get; }
    internal ConnectionAuthenticationMode AuthenticationMode { get; }
    internal AzureClientCredentialsOptions? AzureCredentials { get; }
    internal OAuthConnectionOptions? OAuthOptions { get; }
    internal IReadOnlyDictionary<string, string> Headers { get; }
    internal HttpClientTransport Transport { get; }
    internal McpClient Client { get; }
    internal IReadOnlyList<McpClientTool> Tools { get; }
    internal IReadOnlyList<McpClientPrompt> Prompts { get; }
    internal IReadOnlyList<McpClientResource> Resources { get; }
    internal IReadOnlyList<McpClientResourceTemplate> ResourceTemplates { get; }
    internal bool HasSamplingHandler { get; set; }

    public async ValueTask DisposeAsync()
    {
        await Client.DisposeAsync().ConfigureAwait(false);
        await Transport.DisposeAsync().ConfigureAwait(false);
    }
}
