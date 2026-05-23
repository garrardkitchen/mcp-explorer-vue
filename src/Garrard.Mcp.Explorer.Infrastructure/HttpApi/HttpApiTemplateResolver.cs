using System.Text.RegularExpressions;
using Garrard.Mcp.Explorer.Core.Domain.Connections;
using Garrard.Mcp.Explorer.Core.Domain.HttpApi;

namespace Garrard.Mcp.Explorer.Infrastructure.HttpApi;

public static partial class HttpApiTemplateResolver
{
    [GeneratedRegex(@"\{(?<name>[A-Za-z_][A-Za-z0-9_.-]*)(:(?<default>[^{}]*))?\}", RegexOptions.CultureInvariant)]
    private static partial Regex PlaceholderRegex();

    public static HttpApiDefinition Apply(
        HttpApiDefinition definition,
        IReadOnlyDictionary<string, string?>? inputs)
    {
        var normalizedInputs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (inputs is not null)
        {
            foreach (var (key, value) in inputs)
            {
                if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                normalizedInputs[key] = value;
            }
        }

        return new HttpApiDefinition
        {
            Id = definition.Id,
            Name = definition.Name,
            BaseUrl = ReplacePlaceholders(definition.BaseUrl, normalizedInputs) ?? string.Empty,
            Method = definition.Method,
            Path = ReplacePlaceholders(definition.Path, normalizedInputs) ?? string.Empty,
            AuthenticationMode = definition.AuthenticationMode,
            Headers = [.. definition.Headers.Select(h => new HttpApiHeader
            {
                Name = ReplacePlaceholders(h.Name, normalizedInputs) ?? string.Empty,
                Value = ReplacePlaceholders(h.Value, normalizedInputs) ?? string.Empty
            })],
            QueryParams = [.. definition.QueryParams.Select(q => new HttpApiQueryParam
            {
                Name = ReplacePlaceholders(q.Name, normalizedInputs) ?? string.Empty,
                Value = ReplacePlaceholders(q.Value, normalizedInputs) ?? string.Empty,
                Enabled = q.Enabled
            })],
            BodyTemplate = ReplacePlaceholders(definition.BodyTemplate, normalizedInputs),
            GroupName = definition.GroupName,
            Tags = [.. definition.Tags],
            Note = definition.Note,
            AzureCredentials = definition.AzureCredentials is null
                ? null
                : new HttpApiAzureCredentialsOptions
                {
                    TenantId = ReplacePlaceholders(definition.AzureCredentials.TenantId, normalizedInputs) ?? string.Empty,
                    ClientId = ReplacePlaceholders(definition.AzureCredentials.ClientId, normalizedInputs) ?? string.Empty,
                    ClientSecret = ReplacePlaceholders(definition.AzureCredentials.ClientSecret, normalizedInputs) ?? string.Empty,
                    Scope = ReplacePlaceholders(definition.AzureCredentials.Scope, normalizedInputs) ?? string.Empty,
                    AuthorityHost = ReplacePlaceholders(definition.AzureCredentials.AuthorityHost, normalizedInputs),
                    SubscriptionId = ReplacePlaceholders(definition.AzureCredentials.SubscriptionId, normalizedInputs),
                    KeyVaultSecretRef = definition.AzureCredentials.KeyVaultSecretRef is null
                        ? null
                        : new KeyVaultSecretReference
                        {
                            VaultName = ReplacePlaceholders(definition.AzureCredentials.KeyVaultSecretRef.VaultName, normalizedInputs) ?? string.Empty,
                            SecretName = ReplacePlaceholders(definition.AzureCredentials.KeyVaultSecretRef.SecretName, normalizedInputs) ?? string.Empty
                        }
                },
            ApiKeyOptions = definition.ApiKeyOptions is null
                ? null
                : new HttpApiApiKeyOptions
                {
                    HeaderName = ReplacePlaceholders(definition.ApiKeyOptions.HeaderName, normalizedInputs) ?? string.Empty,
                    ApiKey = ReplacePlaceholders(definition.ApiKeyOptions.ApiKey, normalizedInputs) ?? string.Empty,
                    Prefix = ReplacePlaceholders(definition.ApiKeyOptions.Prefix, normalizedInputs)
                },
            BearerOptions = definition.BearerOptions is null
                ? null
                : new HttpApiBearerOptions
                {
                    Token = ReplacePlaceholders(definition.BearerOptions.Token, normalizedInputs) ?? string.Empty
                },
            GoldenSnapshotId = definition.GoldenSnapshotId,
            CreatedAt = definition.CreatedAt,
            LastUpdatedAt = definition.LastUpdatedAt,
            LastInvokedAt = definition.LastInvokedAt
        };
    }

    private static string? ReplacePlaceholders(string? template, IReadOnlyDictionary<string, string> inputs)
    {
        if (string.IsNullOrEmpty(template))
        {
            return template;
        }

        return PlaceholderRegex().Replace(template, match =>
        {
            var name = match.Groups["name"].Value;
            if (inputs.TryGetValue(name, out var provided) && !string.IsNullOrWhiteSpace(provided))
            {
                return provided;
            }

            var fallback = match.Groups["default"].Value;
            return fallback;
        });
    }
}
