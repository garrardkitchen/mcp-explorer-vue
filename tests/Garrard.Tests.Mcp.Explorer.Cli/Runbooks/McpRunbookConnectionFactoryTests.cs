using Garrard.Mcp.Explorer.Cli.Runbooks;
using Garrard.Mcp.Explorer.Core.Domain.Connections;
using Garrard.Mcp.Explorer.Core.Interfaces;
using Moq;

namespace Garrard.Tests.Mcp.Explorer.Cli.Runbooks;

public class McpRunbookConnectionFactoryTests
{
    [Fact]
    public async Task BuildAsync_Bearer_UsesAuthorizationHeader()
    {
        var resolver = new Mock<IKeyVaultSecretResolver>(MockBehavior.Strict);
        var factory = new McpRunbookConnectionFactory(resolver.Object);

        var connection = new McpRunbookConnection
        {
            Name = "c1",
            Endpoint = "https://example",
            Auth = new McpRunbookAuth { Type = "bearer", Token = "tkn" }
        };

        var def = await factory.BuildAsync(connection, CancellationToken.None);

        Assert.Equal(ConnectionAuthenticationMode.CustomHeaders, def.AuthenticationMode);
        var auth = Assert.Single(def.Headers);
        Assert.Equal("Authorization", auth.Name);
        Assert.Equal("Bearer", auth.AuthorizationType);
        Assert.Equal("tkn", auth.Value);
    }

    [Fact]
    public async Task BuildAsync_ApiKey_ResolvesFromKeyVault()
    {
        var resolver = new Mock<IKeyVaultSecretResolver>();
        resolver
            .Setup(r => r.ResolveAsync(It.IsAny<KeyVaultSecretReference>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("secret-key");

        var factory = new McpRunbookConnectionFactory(resolver.Object);

        var connection = new McpRunbookConnection
        {
            Name = "c1",
            Endpoint = "https://example",
            Auth = new McpRunbookAuth
            {
                Type = "apikey",
                HeaderName = "X-Api-Key",
                ApiKeyFromKeyVault = new KeyVaultSecretReference { VaultName = "kv", SecretName = "api" }
            }
        };

        var def = await factory.BuildAsync(connection, CancellationToken.None);

        var header = Assert.Single(def.Headers);
        Assert.Equal("X-Api-Key", header.Name);
        Assert.Equal("secret-key", header.Value);
    }

    [Fact]
    public async Task BuildAsync_Basic_BuildsBase64Credentials()
    {
        var resolver = new Mock<IKeyVaultSecretResolver>(MockBehavior.Strict);
        var factory = new McpRunbookConnectionFactory(resolver.Object);

        var connection = new McpRunbookConnection
        {
            Name = "c1",
            Endpoint = "https://example",
            Auth = new McpRunbookAuth
            {
                Type = "basic",
                Username = "user",
                Password = "pass"
            }
        };

        var def = await factory.BuildAsync(connection, CancellationToken.None);

        var header = Assert.Single(def.Headers);
        Assert.Equal("Basic", header.AuthorizationType);
        Assert.Equal("dXNlcjpwYXNz", header.Value);
    }

    [Fact]
    public async Task BuildAsync_AzureClientCredentials_SetsAzureMode()
    {
        var resolver = new Mock<IKeyVaultSecretResolver>(MockBehavior.Strict);
        var factory = new McpRunbookConnectionFactory(resolver.Object);

        var connection = new McpRunbookConnection
        {
            Name = "c1",
            Endpoint = "https://example",
            Auth = new McpRunbookAuth
            {
                Type = "azureClientCredentials",
                TenantId = "tenant",
                ClientId = "client",
                Scope = "api://x/.default",
                ClientSecretFromKeyVault = new KeyVaultSecretReference { VaultName = "kv", SecretName = "client-secret" }
            }
        };

        var def = await factory.BuildAsync(connection, CancellationToken.None);

        Assert.Equal(ConnectionAuthenticationMode.AzureClientCredentials, def.AuthenticationMode);
        Assert.NotNull(def.AzureCredentials);
        Assert.Equal("client", def.AzureCredentials!.ClientId);
        Assert.Equal("tenant", def.AzureCredentials.TenantId);
        Assert.Equal("api://x/.default", def.AzureCredentials.Scope);
        Assert.NotNull(def.AzureCredentials.KeyVaultSecretRef);
    }
}
