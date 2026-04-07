using Garrard.Mcp.Explorer.Core.Domain.Connections;
using Garrard.Mcp.Explorer.Core.Interfaces;

namespace Garrard.Tests.Mcp.Explorer.Infrastructure.Azure;

/// <summary>
/// Tests for <see cref="KeyVaultSecretReference"/> domain model.
/// Verifies the reference representation, equality semantics, and <see cref="KeyVaultSecretReference.ToString"/>.
/// </summary>
public class KeyVaultSecretReferenceTests
{
    [Fact]
    public void ToString_ReturnsCombinedVaultAndSecretName()
    {
        var reference = new KeyVaultSecretReference
        {
            VaultName = "kv-prod",
            SecretName = "api-secret"
        };

        Assert.Equal("kv-prod/api-secret", reference.ToString());
    }

    [Fact]
    public void ToString_EmptyFields_ReturnsSlashOnly()
    {
        var reference = new KeyVaultSecretReference();
        Assert.Equal("/", reference.ToString());
    }

    [Fact]
    public void Records_WithSameValues_AreEqual()
    {
        var a = new KeyVaultSecretReference { VaultName = "vault", SecretName = "secret" };
        var b = new KeyVaultSecretReference { VaultName = "vault", SecretName = "secret" };
        Assert.Equal(a, b);
    }

    [Fact]
    public void Records_WithDifferentVaultName_AreNotEqual()
    {
        var a = new KeyVaultSecretReference { VaultName = "vault-a", SecretName = "secret" };
        var b = new KeyVaultSecretReference { VaultName = "vault-b", SecretName = "secret" };
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Records_WithDifferentSecretName_AreNotEqual()
    {
        var a = new KeyVaultSecretReference { VaultName = "vault", SecretName = "secret-a" };
        var b = new KeyVaultSecretReference { VaultName = "vault", SecretName = "secret-b" };
        Assert.NotEqual(a, b);
    }
}
