using Garrard.Mcp.Explorer.Core.Domain.Connections;

namespace Garrard.Mcp.Explorer.Infrastructure.Mcp;

/// <summary>
/// Pure header-building logic extracted from <see cref="ConnectionService"/> for testability.
/// Converts a list of <see cref="ConnectionHeader"/> into a string-keyed header dictionary,
/// automatically applying an authorization scheme prefix when configured.
/// </summary>
public static class ConnectionHeaderBuilder
{
    private const string DefaultAuthorizationScheme = "Bearer";
    private const string RawAuthorizationScheme = "None";
    private static readonly string[] KnownAuthorizationSchemes = ["Bearer", "Basic"];

    /// <summary>
    /// Converts a sequence of connection headers into a <c>Dictionary&lt;string, string&gt;</c>.
    /// For <see cref="ConnectionHeader.IsAuthorization"/> headers the <see cref="ConnectionHeader.AuthorizationType"/>
    /// is prepended to the value. Blank types default to <c>Bearer</c>, while <c>None</c> preserves the raw value.
    /// Headers with a blank name or blank value are skipped.
    /// </summary>
    public static Dictionary<string, string> Build(IEnumerable<ConnectionHeader> headers)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var header in headers)
        {
            var name = header.Name?.Trim();
            if (string.IsNullOrWhiteSpace(name)) continue;

            var value = header.Value ?? string.Empty;
            if (string.IsNullOrWhiteSpace(value)) continue;

            if (header.IsAuthorization)
            {
                result[name] = BuildAuthorizationValue(header);
            }
            else
            {
                result[name] = value;
            }
        }

        return result;
    }

    private static string BuildAuthorizationValue(ConnectionHeader header)
    {
        var value = (header.Value ?? string.Empty).Trim();
        var scheme = header.AuthorizationType?.Trim();

        if (string.Equals(scheme, RawAuthorizationScheme, StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        if (string.IsNullOrWhiteSpace(scheme))
        {
            return StartsWithKnownAuthorizationScheme(value)
                ? value
                : $"{DefaultAuthorizationScheme} {value}";
        }

        return StartsWithAuthorizationScheme(value, scheme)
            ? value
            : $"{scheme} {value}";
    }

    private static bool StartsWithKnownAuthorizationScheme(string value)
        => KnownAuthorizationSchemes.Any(scheme => StartsWithAuthorizationScheme(value, scheme));

    private static bool StartsWithAuthorizationScheme(string value, string scheme)
    {
        var trimmed = value.TrimStart();
        return trimmed.Length >= scheme.Length
               && trimmed.StartsWith(scheme, StringComparison.OrdinalIgnoreCase)
               && (trimmed.Length == scheme.Length || char.IsWhiteSpace(trimmed[scheme.Length]));
    }
}
