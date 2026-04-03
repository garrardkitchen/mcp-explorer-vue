using Garrard.Mcp.Explorer.Core.Domain.Connections;
using Garrard.Mcp.Explorer.Infrastructure.Mcp;

namespace Garrard.Tests.Mcp.Explorer.Infrastructure.Mcp;

/// <summary>
/// Tests for <see cref="ConnectionHeaderBuilder.Build"/>.
/// Verifies the scheme-prefix injection logic for Authorization headers and
/// pass-through behaviour for non-authorization headers.
/// </summary>
public class ConnectionHeaderBuilderTests
{
    // ── Empty input ───────────────────────────────────────────────────────────

    [Fact]
    public void Build_EmptyList_ReturnsEmptyDictionary()
    {
        var result = ConnectionHeaderBuilder.Build([]);

        Assert.Empty(result);
    }

    // ── Authorization header — default scheme ─────────────────────────────────

    [Fact]
    public void Build_AuthorizationHeader_NoScheme_AddsBearerPrefix()
    {
        var headers = new List<ConnectionHeader>
        {
            new() { Name = "Authorization", Value = "my-api-key", AuthorizationType = "" }
        };

        var result = ConnectionHeaderBuilder.Build(headers);

        Assert.Equal("Bearer my-api-key", result["Authorization"]);
    }

    [Fact]
    public void Build_AuthorizationHeader_NullScheme_AddsBearerPrefix()
    {
        var headers = new List<ConnectionHeader>
        {
            new() { Name = "Authorization", Value = "token123" }
            // AuthorizationType defaults to string.Empty
        };

        var result = ConnectionHeaderBuilder.Build(headers);

        Assert.Equal("Bearer token123", result["Authorization"]);
    }

    // ── Authorization header — explicit Bearer scheme ─────────────────────────

    [Fact]
    public void Build_AuthorizationHeader_ExplicitBearerScheme_AddsBearerPrefix()
    {
        var headers = new List<ConnectionHeader>
        {
            new() { Name = "Authorization", Value = "my-jwt-token", AuthorizationType = "Bearer" }
        };

        var result = ConnectionHeaderBuilder.Build(headers);

        Assert.Equal("Bearer my-jwt-token", result["Authorization"]);
    }

    // ── Authorization header — Basic scheme ───────────────────────────────────

    [Fact]
    public void Build_AuthorizationHeader_BasicScheme_AddsBasicPrefix()
    {
        var headers = new List<ConnectionHeader>
        {
            new() { Name = "Authorization", Value = "dXNlcjpwYXNz", AuthorizationType = "Basic" }
        };

        var result = ConnectionHeaderBuilder.Build(headers);

        Assert.Equal("Basic dXNlcjpwYXNz", result["Authorization"]);
    }

    // ── Authorization header — case-insensitive name matching ─────────────────

    [Fact]
    public void Build_AuthorizationHeader_LowercaseName_IsRecognisedAsAuthorizationHeader()
    {
        var headers = new List<ConnectionHeader>
        {
            new() { Name = "authorization", Value = "token-abc", AuthorizationType = "Bearer" }
        };

        var result = ConnectionHeaderBuilder.Build(headers);

        Assert.True(result.ContainsKey("authorization"));
        Assert.Equal("Bearer token-abc", result["authorization"]);
    }

    // ── Non-authorization headers ─────────────────────────────────────────────

    [Fact]
    public void Build_NonAuthorizationHeader_PassesThroughUnchanged()
    {
        var headers = new List<ConnectionHeader>
        {
            new() { Name = "X-Api-Version", Value = "2024-01-01" }
        };

        var result = ConnectionHeaderBuilder.Build(headers);

        Assert.Equal("2024-01-01", result["X-Api-Version"]);
    }

    [Fact]
    public void Build_MultipleNonAuthHeaders_AllPassThrough()
    {
        var headers = new List<ConnectionHeader>
        {
            new() { Name = "X-Tenant-Id", Value = "tenant-123" },
            new() { Name = "X-Correlation-Id", Value = "corr-abc" }
        };

        var result = ConnectionHeaderBuilder.Build(headers);

        Assert.Equal(2, result.Count);
        Assert.Equal("tenant-123", result["X-Tenant-Id"]);
        Assert.Equal("corr-abc", result["X-Correlation-Id"]);
    }

    // ── Mixed headers ─────────────────────────────────────────────────────────

    [Fact]
    public void Build_MixedHeaders_AuthorizationAndCustom_BothPresent()
    {
        var headers = new List<ConnectionHeader>
        {
            new() { Name = "Authorization", Value = "sk-123", AuthorizationType = "Bearer" },
            new() { Name = "X-Custom", Value = "custom-value" }
        };

        var result = ConnectionHeaderBuilder.Build(headers);

        Assert.Equal(2, result.Count);
        Assert.Equal("Bearer sk-123", result["Authorization"]);
        Assert.Equal("custom-value", result["X-Custom"]);
    }

    // ── Blank / whitespace headers are skipped ────────────────────────────────

    [Fact]
    public void Build_HeaderWithBlankName_IsSkipped()
    {
        var headers = new List<ConnectionHeader>
        {
            new() { Name = "  ", Value = "some-value" },
            new() { Name = "X-Valid", Value = "ok" }
        };

        var result = ConnectionHeaderBuilder.Build(headers);

        Assert.Single(result);
        Assert.Equal("ok", result["X-Valid"]);
    }

    [Fact]
    public void Build_HeaderWithBlankValue_IsSkipped()
    {
        var headers = new List<ConnectionHeader>
        {
            new() { Name = "X-Empty", Value = "   " },
            new() { Name = "X-Valid", Value = "present" }
        };

        var result = ConnectionHeaderBuilder.Build(headers);

        Assert.Single(result);
        Assert.Equal("present", result["X-Valid"]);
    }
}
