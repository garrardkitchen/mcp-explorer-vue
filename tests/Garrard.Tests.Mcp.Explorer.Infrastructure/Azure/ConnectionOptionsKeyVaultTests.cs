using Garrard.Mcp.Explorer.Core.Domain.Connections;

namespace Garrard.Tests.Mcp.Explorer.Infrastructure.Azure;

/// <summary>
/// Verifies the Key Vault reference properties on the connection options domain models.
/// </summary>
public class ConnectionOptionsKeyVaultTests
{
    [Fact]
    public void AzureClientCredentialsOptions_KeyVaultSecretRef_DefaultsToNull()
    {
        var opts = new AzureClientCredentialsOptions();
        Assert.Null(opts.KeyVaultSecretRef);
    }

    [Fact]
    public void AzureClientCredentialsOptions_KeyVaultSecretRef_CanBeSet()
    {
        var kvRef = new KeyVaultSecretReference { VaultName = "kv-test", SecretName = "my-secret" };
        var opts = new AzureClientCredentialsOptions { KeyVaultSecretRef = kvRef };

        Assert.NotNull(opts.KeyVaultSecretRef);
        Assert.Equal("kv-test", opts.KeyVaultSecretRef.VaultName);
        Assert.Equal("my-secret", opts.KeyVaultSecretRef.SecretName);
    }

    [Fact]
    public void OAuthConnectionOptions_KeyVaultSecretRef_DefaultsToNull()
    {
        var opts = new OAuthConnectionOptions();
        Assert.Null(opts.KeyVaultSecretRef);
    }

    [Fact]
    public void OAuthConnectionOptions_KeyVaultSecretRef_CanBeSet()
    {
        var kvRef = new KeyVaultSecretReference { VaultName = "kv-oauth", SecretName = "oauth-secret" };
        var opts = new OAuthConnectionOptions { KeyVaultSecretRef = kvRef };

        Assert.NotNull(opts.KeyVaultSecretRef);
        Assert.Equal("kv-oauth", opts.KeyVaultSecretRef.VaultName);
        Assert.Equal("oauth-secret", opts.KeyVaultSecretRef.SecretName);
    }

    [Fact]
    public void AzureClientCredentialsOptions_WhenKeyVaultRefSet_ClientSecretStillAccessible()
    {
        // Both can coexist; the resolver takes precedence at runtime.
        var opts = new AzureClientCredentialsOptions
        {
            ClientSecret = "fallback",
            KeyVaultSecretRef = new KeyVaultSecretReference { VaultName = "vault", SecretName = "secret" }
        };

        Assert.Equal("fallback", opts.ClientSecret);
        Assert.NotNull(opts.KeyVaultSecretRef);
    }
}
