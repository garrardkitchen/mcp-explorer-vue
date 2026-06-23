using System.Text;
using Garrard.Mcp.Explorer.Core.Domain.Connections;
using Garrard.Mcp.Explorer.Core.Interfaces;

namespace Garrard.Mcp.Explorer.Cli.Runbooks;

public sealed class McpRunbookConnectionFactory
{
    private readonly IKeyVaultSecretResolver _secretResolver;

    public McpRunbookConnectionFactory(IKeyVaultSecretResolver secretResolver)
    {
        _secretResolver = secretResolver;
    }

    public async Task<ConnectionDefinition> BuildAsync(McpRunbookConnection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var authType = connection.Auth.Type.Trim();
        var definition = new ConnectionDefinition
        {
            Name = connection.Name,
            Endpoint = connection.Endpoint,
            AuthenticationMode = ConnectionAuthenticationMode.CustomHeaders
        };

        switch (authType.ToLowerInvariant())
        {
            case "bearer":
            {
                var token = await ResolveSecretValueAsync(connection.Auth.Token, connection.Auth.TokenFromKeyVault, cancellationToken).ConfigureAwait(false);
                definition.Headers.Add(new ConnectionHeader
                {
                    Name = "Authorization",
                    Value = token,
                    AuthorizationType = "Bearer"
                });
                break;
            }
            case "apikey":
            {
                var apiKey = await ResolveSecretValueAsync(connection.Auth.ApiKey, connection.Auth.ApiKeyFromKeyVault, cancellationToken).ConfigureAwait(false);
                var headerName = string.IsNullOrWhiteSpace(connection.Auth.HeaderName) ? "X-Api-Key" : connection.Auth.HeaderName.Trim();
                var value = string.IsNullOrEmpty(connection.Auth.Prefix) ? apiKey : $"{connection.Auth.Prefix}{apiKey}";
                definition.Headers.Add(new ConnectionHeader
                {
                    Name = headerName,
                    Value = value,
                    AuthorizationType = string.Equals(headerName, "Authorization", StringComparison.OrdinalIgnoreCase) ? "None" : string.Empty
                });
                break;
            }
            case "basic":
            {
                var username = connection.Auth.Username ?? string.Empty;
                var password = await ResolveSecretValueAsync(connection.Auth.Password, connection.Auth.PasswordFromKeyVault, cancellationToken).ConfigureAwait(false);
                var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
                definition.Headers.Add(new ConnectionHeader
                {
                    Name = "Authorization",
                    Value = encoded,
                    AuthorizationType = "Basic"
                });
                break;
            }
            case "customheaders":
            case "custom":
            {
                foreach (var header in connection.Auth.Headers)
                {
                    definition.Headers.Add(new ConnectionHeader
                    {
                        Name = header.Name,
                        Value = header.Value,
                        AuthorizationType = header.AuthorizationType ?? string.Empty
                    });
                }
                break;
            }
            case "azureclientcredentials":
            {
                definition.AuthenticationMode = ConnectionAuthenticationMode.AzureClientCredentials;

                var clientId = await ResolveSecretValueOptionalAsync(connection.Auth.ClientId, connection.Auth.ClientIdFromKeyVault, cancellationToken).ConfigureAwait(false);
                var clientSecret = connection.Auth.ClientSecret ?? string.Empty;

                definition.AzureCredentials = new AzureClientCredentialsOptions
                {
                    TenantId = connection.Auth.TenantId ?? string.Empty,
                    ClientId = clientId ?? string.Empty,
                    ClientSecret = clientSecret,
                    Scope = connection.Auth.Scope ?? string.Empty,
                    AuthorityHost = connection.Auth.AuthorityHost,
                    KeyVaultSecretRef = connection.Auth.ClientSecretFromKeyVault
                };
                break;
            }
            default:
                throw new InvalidOperationException($"Unsupported auth type '{connection.Auth.Type}'.");
        }

        return definition;
    }

    private async Task<string> ResolveSecretValueAsync(string? literal, KeyVaultSecretReference? reference, CancellationToken cancellationToken)
    {
        var value = await ResolveSecretValueOptionalAsync(literal, reference, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("Secret value cannot be empty.");

        return value;
    }

    private async Task<string?> ResolveSecretValueOptionalAsync(string? literal, KeyVaultSecretReference? reference, CancellationToken cancellationToken)
    {
        if (reference is not null)
            return await _secretResolver.ResolveAsync(reference, cancellationToken).ConfigureAwait(false);

        return literal;
    }
}
