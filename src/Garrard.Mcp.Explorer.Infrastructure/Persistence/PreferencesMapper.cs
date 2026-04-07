using Garrard.Mcp.Explorer.Core.Domain.Connections;
using Garrard.Mcp.Explorer.Core.Domain.LlmModels;
using Garrard.Mcp.Explorer.Core.Domain.Preferences;
using Garrard.Mcp.Explorer.Core.Interfaces;

namespace Garrard.Mcp.Explorer.Infrastructure.Persistence;

/// <summary>
/// Maps between the in-memory (decrypted) and persisted (encrypted) forms of <see cref="UserPreferences"/>.
/// </summary>
internal static class PreferencesMapper
{
    internal static UserPreferences Encrypt(UserPreferences prefs, ISecretProtector protector)
    {
        var encConnections = prefs.Connections.Select(c => EncryptConnection(c, protector)).ToList();
        var encModels = prefs.LlmModels.Select(m => EncryptModel(m, protector)).ToList();

        return prefs with
        {
            Connections = encConnections,
            LlmModels = encModels
        };
    }

    internal static UserPreferences Decrypt(UserPreferences prefs, ISecretProtector protector)
    {
        var decConnections = prefs.Connections.Select(c => DecryptConnection(c, protector)).ToList();
        var decModels = prefs.LlmModels.Select(m => DecryptModel(m, protector)).ToList();

        return prefs with
        {
            Connections = decConnections,
            LlmModels = decModels
        };
    }

    private static ConnectionDefinition EncryptConnection(ConnectionDefinition c, ISecretProtector p)
    {
        var encHeaders = (c.Headers ?? [])
            .Select(h => !string.IsNullOrEmpty(h.Value)
                ? new ConnectionHeader { Name = h.Name, Value = p.Encrypt(h.Value), AuthorizationType = h.AuthorizationType }
                : h)
            .ToList();

        AzureClientCredentialsOptions? encAzure = null;
        if (c.AzureCredentials is not null)
        {
            encAzure = new AzureClientCredentialsOptions
            {
                TenantId = c.AzureCredentials.TenantId,
                ClientId = c.AzureCredentials.ClientId,
                ClientSecret = string.IsNullOrEmpty(c.AzureCredentials.ClientSecret)
                    ? c.AzureCredentials.ClientSecret
                    : p.Encrypt(c.AzureCredentials.ClientSecret),
                Scope = c.AzureCredentials.Scope,
                AuthorityHost = c.AzureCredentials.AuthorityHost,
                // KV reference is never encrypted — it contains no secret value
                KeyVaultSecretRef = c.AzureCredentials.KeyVaultSecretRef
            };
        }

        OAuthConnectionOptions? encOAuth = null;
        if (c.OAuthOptions is not null)
        {
            encOAuth = new OAuthConnectionOptions
            {
                ClientId = c.OAuthOptions.ClientId,
                ClientSecret = string.IsNullOrEmpty(c.OAuthOptions.ClientSecret)
                    ? c.OAuthOptions.ClientSecret
                    : p.Encrypt(c.OAuthOptions.ClientSecret),
                RedirectUri = c.OAuthOptions.RedirectUri,
                Scopes = c.OAuthOptions.Scopes,
                ClientMetadataDocumentUri = c.OAuthOptions.ClientMetadataDocumentUri,
                KeyVaultSecretRef = c.OAuthOptions.KeyVaultSecretRef
            };
        }

        return new ConnectionDefinition
        {
            Name = c.Name,
            Endpoint = c.Endpoint,
            AuthenticationMode = c.AuthenticationMode,
            Headers = encHeaders,
            AzureCredentials = encAzure,
            OAuthOptions = encOAuth,
            Note = c.Note,
            GroupName = c.GroupName,
            CreatedAt = c.CreatedAt,
            LastUpdatedAt = c.LastUpdatedAt,
            LastUsedAt = c.LastUsedAt
        };
    }

    private static ConnectionDefinition DecryptConnection(ConnectionDefinition c, ISecretProtector p)
    {
        var decHeaders = (c.Headers ?? [])
            .Select(h => !string.IsNullOrEmpty(h.Value)
                ? new ConnectionHeader { Name = h.Name, Value = p.Decrypt(h.Value), AuthorizationType = h.AuthorizationType }
                : h)
            .ToList();

        AzureClientCredentialsOptions? decAzure = null;
        if (c.AzureCredentials is not null)
        {
            decAzure = new AzureClientCredentialsOptions
            {
                TenantId = c.AzureCredentials.TenantId,
                ClientId = c.AzureCredentials.ClientId,
                ClientSecret = string.IsNullOrEmpty(c.AzureCredentials.ClientSecret)
                    ? c.AzureCredentials.ClientSecret
                    : p.Decrypt(c.AzureCredentials.ClientSecret),
                Scope = c.AzureCredentials.Scope,
                AuthorityHost = c.AzureCredentials.AuthorityHost,
                KeyVaultSecretRef = c.AzureCredentials.KeyVaultSecretRef
            };
        }

        OAuthConnectionOptions? decOAuth = null;
        if (c.OAuthOptions is not null)
        {
            decOAuth = new OAuthConnectionOptions
            {
                ClientId = c.OAuthOptions.ClientId,
                ClientSecret = string.IsNullOrEmpty(c.OAuthOptions.ClientSecret)
                    ? c.OAuthOptions.ClientSecret
                    : p.Decrypt(c.OAuthOptions.ClientSecret),
                RedirectUri = c.OAuthOptions.RedirectUri,
                Scopes = c.OAuthOptions.Scopes,
                ClientMetadataDocumentUri = c.OAuthOptions.ClientMetadataDocumentUri,
                KeyVaultSecretRef = c.OAuthOptions.KeyVaultSecretRef
            };
        }

        return new ConnectionDefinition
        {
            Name = c.Name,
            Endpoint = c.Endpoint,
            AuthenticationMode = c.AuthenticationMode,
            Headers = decHeaders,
            AzureCredentials = decAzure,
            OAuthOptions = decOAuth,
            Note = c.Note,
            GroupName = c.GroupName,
            CreatedAt = c.CreatedAt,
            LastUpdatedAt = c.LastUpdatedAt,
            LastUsedAt = c.LastUsedAt
        };
    }

    private static LlmModelDefinition EncryptModel(LlmModelDefinition m, ISecretProtector p)
    {
        return new LlmModelDefinition
        {
            Name = m.Name,
            ProviderType = m.ProviderType,
            Endpoint = m.Endpoint,
            ApiKey = string.IsNullOrEmpty(m.ApiKey) ? m.ApiKey : p.Encrypt(m.ApiKey),
            ModelName = m.ModelName,
            SystemPrompt = m.SystemPrompt,
            DeploymentName = m.DeploymentName,
            Note = m.Note
        };
    }

    private static LlmModelDefinition DecryptModel(LlmModelDefinition m, ISecretProtector p)
    {
        return new LlmModelDefinition
        {
            Name = m.Name,
            ProviderType = m.ProviderType,
            Endpoint = m.Endpoint,
            ApiKey = string.IsNullOrEmpty(m.ApiKey) ? m.ApiKey : p.Decrypt(m.ApiKey),
            ModelName = m.ModelName,
            SystemPrompt = m.SystemPrompt,
            DeploymentName = m.DeploymentName,
            Note = m.Note
        };
    }
}
