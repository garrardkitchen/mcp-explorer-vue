using Garrard.Mcp.Explorer.Core.Domain.Connections;

namespace Garrard.Tests.Mcp.Explorer.Core.Domain.Connections;

public class ConnectionDefinitionTests
{
    [Fact]
    public void DefaultConstruction_SetsExpectedDefaults()
    {
        var def = new ConnectionDefinition();

        Assert.Equal(string.Empty, def.Name);
        Assert.Equal(string.Empty, def.Endpoint);
        Assert.Equal(ConnectionAuthenticationMode.CustomHeaders, def.AuthenticationMode);
        Assert.Equal(string.Empty, def.Note);
        Assert.Null(def.GroupName);
        Assert.Null(def.AzureCredentials);
        Assert.Null(def.OAuthOptions);
        Assert.Null(def.LastUpdatedAt);
        Assert.Null(def.LastUsedAt);
    }

    [Fact]
    public void DefaultConstruction_HeadersListIsEmpty()
    {
        var def = new ConnectionDefinition();

        Assert.NotNull(def.Headers);
        Assert.Empty(def.Headers);
    }

    [Fact]
    public void CanSetAllProperties()
    {
        var now = DateTime.UtcNow;
        var def = new ConnectionDefinition
        {
            Name = "my-server",
            Endpoint = "https://mcp.example.com/sse",
            AuthenticationMode = ConnectionAuthenticationMode.AzureClientCredentials,
            Note = "Production MCP server",
            GroupName = "Production",
            CreatedAt = now,
            LastUpdatedAt = now,
            LastUsedAt = now
        };

        Assert.Equal("my-server", def.Name);
        Assert.Equal("https://mcp.example.com/sse", def.Endpoint);
        Assert.Equal(ConnectionAuthenticationMode.AzureClientCredentials, def.AuthenticationMode);
        Assert.Equal("Production MCP server", def.Note);
        Assert.Equal("Production", def.GroupName);
        Assert.Equal(now, def.CreatedAt);
        Assert.Equal(now, def.LastUpdatedAt);
        Assert.Equal(now, def.LastUsedAt);
    }

    [Fact]
    public void Headers_CanBeInitialisedWithValues()
    {
        var headers = new List<ConnectionHeader>
        {
            new() { Name = "Authorization", Value = "my-key", AuthorizationType = "Bearer" },
            new() { Name = "X-Custom-Header", Value = "custom-value" }
        };

        var def = new ConnectionDefinition { Headers = headers };

        Assert.Equal(2, def.Headers.Count);
        Assert.Equal("Authorization", def.Headers[0].Name);
        Assert.Equal("X-Custom-Header", def.Headers[1].Name);
    }

    [Fact]
    public void TwoDefinitions_WithSameName_AreNotReferenceEqual()
    {
        var def1 = new ConnectionDefinition { Name = "server-a" };
        var def2 = new ConnectionDefinition { Name = "server-a" };

        // ConnectionDefinition is a class — equality is by reference
        Assert.NotSame(def1, def2);
        Assert.False(ReferenceEquals(def1, def2));
    }

    [Fact]
    public void AuthenticationMode_SupportsAllValues()
    {
        foreach (ConnectionAuthenticationMode mode in Enum.GetValues<ConnectionAuthenticationMode>())
        {
            var def = new ConnectionDefinition { AuthenticationMode = mode };
            Assert.Equal(mode, def.AuthenticationMode);
        }
    }

    [Fact]
    public void CreatedAt_DefaultsToRecentUtcTime()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var def = new ConnectionDefinition();
        var after = DateTime.UtcNow.AddSeconds(1);

        Assert.InRange(def.CreatedAt, before, after);
    }
}
