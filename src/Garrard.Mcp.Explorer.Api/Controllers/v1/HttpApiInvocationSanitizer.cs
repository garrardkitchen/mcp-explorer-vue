using System.Text.RegularExpressions;
using System.Text.Json.Nodes;

namespace Garrard.Mcp.Explorer.Api.Controllers.v1;

internal static class HttpApiInvocationSanitizer
{
    private const string RedactedValue = "***REDACTED***";
    private static readonly Regex SensitiveNameRegex = new(
        "(token|secret|password|api[-_]?key|client[-_]?secret|authorization|cookie|session|credential)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SensitiveInlineValueRegex = new(
        "(?<key>authorization|access[_-]?token|refresh[_-]?token|id[_-]?token|api[_-]?key|client[_-]?secret|password|secret|cookie)\\s*(?<sep>[:=])\\s*(?<value>[^\\r\\n,;&]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex QueryValueRegex = new(
        "(?<prefix>[?&])(?<key>[^=&?#]+)=(?<value>[^&#]*)",
        RegexOptions.Compiled);

    private static readonly Regex SecretTokenSegmentRegex = new(
        "^[A-Za-z0-9_\\-.~]{24,}$",
        RegexOptions.Compiled);

    private static readonly HashSet<string> SensitiveHeaderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Authorization",
        "Proxy-Authorization",
        "Cookie",
        "Set-Cookie",
        "X-Api-Key",
        "Api-Key",
        "Ocp-Apim-Subscription-Key"
    };

    public static Dictionary<string, string> SanitizeHeaders(Dictionary<string, string> headers)
    {
        return headers.ToDictionary(
            pair => pair.Key,
            pair =>
            {
                if (IsSensitiveName(pair.Key))
                {
                    return RedactedValue;
                }

                if (string.Equals(pair.Key, "Location", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(pair.Key, "Content-Location", StringComparison.OrdinalIgnoreCase))
                {
                    return SanitizeUrlComponent(pair.Value);
                }

                return pair.Value;
            },
            StringComparer.OrdinalIgnoreCase);
    }

    public static Dictionary<string, string> SanitizeQueryParams(Dictionary<string, string> queryParams)
    {
        return queryParams.ToDictionary(
            pair => pair.Key,
            pair => IsSensitiveName(pair.Key) ? RedactedValue : pair.Value,
            StringComparer.OrdinalIgnoreCase);
    }

    public static string? SanitizeBody(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return body;
        }

        if (TrySanitizeJsonBody(body, out var sanitizedJson))
        {
            return sanitizedJson;
        }

        return SensitiveInlineValueRegex.Replace(body, match =>
        {
            var key = match.Groups["key"].Value;
            var separator = match.Groups["sep"].Value;
            return $"{key}{separator}{RedactedValue}";
        });
    }

    public static string SanitizeUrlComponent(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var redacted = SensitiveInlineValueRegex.Replace(value, match =>
        {
            var key = match.Groups["key"].Value;
            var separator = match.Groups["sep"].Value;
            return $"{key}{separator}{RedactedValue}";
        });

        redacted = QueryValueRegex.Replace(redacted, match =>
        {
            var prefix = match.Groups["prefix"].Value;
            var key = Uri.UnescapeDataString(match.Groups["key"].Value);
            var rawKey = match.Groups["key"].Value;
            var valuePart = match.Groups["value"].Value;
            var sanitizedValue = IsSensitiveQueryName(key) ? Uri.EscapeDataString(RedactedValue) : valuePart;
            return $"{prefix}{rawKey}={sanitizedValue}";
        });

        if (Uri.TryCreate(redacted, UriKind.Absolute, out var absoluteUri))
        {
            var builder = new UriBuilder(absoluteUri);
            if (!string.IsNullOrEmpty(builder.UserName) || !string.IsNullOrEmpty(builder.Password))
            {
                builder.UserName = RedactedValue;
                builder.Password = RedactedValue;
            }

            builder.Path = SanitizePathSegments(builder.Path);
            return builder.Uri.ToString();
        }

        var fragmentIndex = redacted.IndexOf('#');
        var fragment = fragmentIndex >= 0 ? redacted[fragmentIndex..] : string.Empty;
        var withoutFragment = fragmentIndex >= 0 ? redacted[..fragmentIndex] : redacted;
        var queryIndex = withoutFragment.IndexOf('?');
        var pathPart = queryIndex >= 0 ? withoutFragment[..queryIndex] : withoutFragment;
        var queryPart = queryIndex >= 0 ? withoutFragment[queryIndex..] : string.Empty;

        pathPart = SanitizePathSegments(pathPart);
        redacted = $"{pathPart}{queryPart}{fragment}";
        return redacted;
    }

    private static bool TrySanitizeJsonBody(string body, out string sanitizedBody)
    {
        sanitizedBody = body;
        if (!LooksLikeJson(body))
        {
            return false;
        }

        try
        {
            var node = JsonNode.Parse(body);
            if (node is null)
            {
                return false;
            }

            SanitizeNode(node);
            sanitizedBody = node.ToJsonString();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void SanitizeNode(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            foreach (var property in obj.ToList())
            {
                if (property.Value is null)
                {
                    continue;
                }

                if (IsSensitiveName(property.Key))
                {
                    obj[property.Key] = RedactedValue;
                    continue;
                }

                SanitizeNode(property.Value);
            }
            return;
        }

        if (node is JsonArray arr)
        {
            foreach (var item in arr)
            {
                if (item is not null)
                {
                    SanitizeNode(item);
                }
            }
        }
    }

    private static bool IsSensitiveName(string name)
    {
        return SensitiveHeaderNames.Contains(name) || SensitiveNameRegex.IsMatch(name);
    }

    private static bool IsSensitiveQueryName(string name)
    {
        return IsSensitiveName(name)
               || string.Equals(name, "code", StringComparison.OrdinalIgnoreCase)
               || string.Equals(name, "state", StringComparison.OrdinalIgnoreCase)
               || string.Equals(name, "session_state", StringComparison.OrdinalIgnoreCase)
               || string.Equals(name, "id_token_hint", StringComparison.OrdinalIgnoreCase);
    }

    private static string SanitizePathSegments(string path)
    {
        var segments = path.Split('/', StringSplitOptions.None);
        for (var i = 0; i < segments.Length; i++)
        {
            var segment = Uri.UnescapeDataString(segments[i]);
            if (string.IsNullOrWhiteSpace(segment))
            {
                continue;
            }

            var previousSegment = i > 0 ? Uri.UnescapeDataString(segments[i - 1]) : string.Empty;
            if (IsSensitiveName(segment) || IsSensitiveName(previousSegment) || LooksLikeSecretToken(segment))
            {
                segments[i] = Uri.EscapeDataString(RedactedValue);
            }
        }

        return string.Join('/', segments);
    }

    private static bool LooksLikeSecretToken(string segment)
    {
        if (Guid.TryParse(segment, out _))
        {
            return false;
        }

        if (segment.All(char.IsDigit))
        {
            return false;
        }

        return SecretTokenSegmentRegex.IsMatch(segment);
    }

    private static bool LooksLikeJson(string value)
    {
        foreach (var ch in value)
        {
            if (char.IsWhiteSpace(ch))
            {
                continue;
            }

            return ch is '{' or '[';
        }

        return false;
    }
}
