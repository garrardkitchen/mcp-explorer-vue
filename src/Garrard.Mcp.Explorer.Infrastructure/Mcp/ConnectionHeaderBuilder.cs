using Garrard.Mcp.Explorer.Core.Domain.Connections;

namespace Garrard.Mcp.Explorer.Infrastructure.Mcp;

/// <summary>
/// Pure header-building logic extracted from <see cref="ConnectionService"/> for testability.
/// Converts a list of <see cref="ConnectionHeader"/> into a string-keyed header dictionary,
/// automatically applying the correct authorization scheme prefix.
/// </summary>
public static class ConnectionHeaderBuilder
{
    /// <summary>
    /// Converts a sequence of connection headers into a <c>Dictionary&lt;string, string&gt;</c>.
    /// For <see cref="ConnectionHeader.IsAuthorization"/> headers the <see cref="ConnectionHeader.AuthorizationType"/>
    /// (defaulting to <c>Bearer</c>) is prepended to the value.
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
                var scheme = string.IsNullOrWhiteSpace(header.AuthorizationType)
                    ? "Bearer"
                    : header.AuthorizationType.Trim();

                result[name] = string.IsNullOrWhiteSpace(scheme) ? value : $"{scheme} {value}";
            }
            else
            {
                result[name] = value;
            }
        }

        return result;
    }
}
