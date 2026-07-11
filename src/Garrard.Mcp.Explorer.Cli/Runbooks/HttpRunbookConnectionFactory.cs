using System.Text;
using Garrard.Mcp.Explorer.Core.Domain.Connections;
using Garrard.Mcp.Explorer.Core.Domain.HttpApi;
using Garrard.Mcp.Explorer.Core.Interfaces;

namespace Garrard.Mcp.Explorer.Cli.Runbooks;

public sealed class HttpRunbookConnectionFactory
{
    private readonly IKeyVaultSecretResolver _secretResolver;

    public HttpRunbookConnectionFactory(IKeyVaultSecretResolver secretResolver)
    {
        _secretResolver = secretResolver;
    }

    public async Task<HttpApiDefinition> BuildAsync(HttpRunbookConnection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (connection.Auth is null)
            throw new InvalidOperationException($"Connection '{connection.Name}' auth block is required.");

        var authType = (connection.Auth.Type ?? string.Empty).Trim();
        var definition = new HttpApiDefinition
        {
            Name = connection.Name,
            BaseUrl = connection.Endpoint,
            Method = string.IsNullOrWhiteSpace(connection.Method) ? "GET" : connection.Method.Trim().ToUpperInvariant(),
            AuthenticationMode = HttpApiAuthenticationMode.None
        };

        switch (authType.ToLowerInvariant())
        {
            case "":
            case "none":
                break;
            case "bearer":
            {
                var token = await ResolveSecretValueAsync(connection.Auth.Token, connection.Auth.TokenFromKeyVault, cancellationToken).ConfigureAwait(false);
                definition.AuthenticationMode = HttpApiAuthenticationMode.Bearer;
                definition.BearerOptions = new HttpApiBearerOptions { Token = token };
                break;
            }
            case "apikey":
            {
                var apiKey = await ResolveSecretValueAsync(connection.Auth.ApiKey, connection.Auth.ApiKeyFromKeyVault, cancellationToken).ConfigureAwait(false);
                definition.AuthenticationMode = HttpApiAuthenticationMode.ApiKey;
                definition.ApiKeyOptions = new HttpApiApiKeyOptions
                {
                    HeaderName = string.IsNullOrWhiteSpace(connection.Auth.HeaderName) ? "X-Api-Key" : connection.Auth.HeaderName.Trim(),
                    ApiKey = apiKey,
                    Prefix = connection.Auth.Prefix
                };
                break;
            }
            case "basic":
            {
                var username = connection.Auth.Username ?? string.Empty;
                var password = await ResolveSecretValueAsync(connection.Auth.Password, connection.Auth.PasswordFromKeyVault, cancellationToken).ConfigureAwait(false);
                var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
                definition.AuthenticationMode = HttpApiAuthenticationMode.CustomHeaders;
                definition.Headers.Add(new HttpApiHeader
                {
                    Name = "Authorization",
                    Value = $"Basic {encoded}"
                });
                break;
            }
            case "customheaders":
            case "custom":
            {
                definition.AuthenticationMode = HttpApiAuthenticationMode.CustomHeaders;
                foreach (var header in connection.Auth.Headers ?? [])
                {
                    definition.Headers.Add(new HttpApiHeader
                    {
                        Name = header.Name,
                        Value = header.Value
                    });
                }
                break;
            }
            case "azureclientcredentials":
            {
                definition.AuthenticationMode = HttpApiAuthenticationMode.AzureClientCredentials;
                var clientId = await ResolveSecretValueOptionalAsync(connection.Auth.ClientId, connection.Auth.ClientIdFromKeyVault, cancellationToken).ConfigureAwait(false);
                definition.AzureCredentials = new HttpApiAzureCredentialsOptions
                {
                    TenantId = connection.Auth.TenantId ?? string.Empty,
                    ClientId = clientId ?? string.Empty,
                    ClientSecret = connection.Auth.ClientSecret ?? string.Empty,
                    Scope = connection.Auth.Scope ?? string.Empty,
                    AuthorityHost = connection.Auth.AuthorityHost,
                    KeyVaultSecretRef = connection.Auth.ClientSecretFromKeyVault
                };
                break;
            }
            default:
                throw new InvalidOperationException($"Unsupported HTTP auth type '{connection.Auth.Type}'.");
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
