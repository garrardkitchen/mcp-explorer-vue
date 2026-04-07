using Garrard.Mcp.Explorer.Core.Domain.Connections;
using Garrard.Mcp.Explorer.Core.Interfaces;
using Garrard.Mcp.Explorer.Infrastructure.Azure;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Garrard.Tests.Mcp.Explorer.Infrastructure.Azure;

/// <summary>
/// Tests for <see cref="KeyVaultSecretResolver"/>.
/// The resolver wraps DefaultAzureCredential and the Azure SDK SecretClient — both
/// are sealed and cannot be mocked. These tests validate the public contract
/// (argument validation, error wrapping, logging) without requiring live Azure credentials.
/// </summary>
public class KeyVaultSecretResolverTests
{
    private readonly KeyVaultSecretResolver _sut = new(NullLogger<KeyVaultSecretResolver>.Instance);

    // ── Argument validation ───────────────────────────────────────────────────

    [Fact]
    public async Task ResolveAsync_NullVaultName_ThrowsArgumentException()
    {
        var reference = new KeyVaultSecretReference
        {
            VaultName = string.Empty,
            SecretName = "my-secret"
        };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.ResolveAsync(reference));
    }

    [Fact]
    public async Task ResolveAsync_NullSecretName_ThrowsArgumentException()
    {
        var reference = new KeyVaultSecretReference
        {
            VaultName = "my-vault",
            SecretName = string.Empty
        };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.ResolveAsync(reference));
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("")]
    public async Task ResolveAsync_WhitespaceVaultName_ThrowsArgumentException(string vaultName)
    {
        var reference = new KeyVaultSecretReference
        {
            VaultName = vaultName,
            SecretName = "my-secret"
        };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.ResolveAsync(reference));
    }
}
