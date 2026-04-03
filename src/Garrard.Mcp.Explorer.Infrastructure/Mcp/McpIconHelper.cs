namespace Garrard.Mcp.Explorer.Infrastructure.Mcp;

/// <summary>
/// Extracts the best icon URL from a <c>ProtocolTool</c>, <c>ProtocolPrompt</c>, or
/// <c>ProtocolResource</c> object using reflection. The MCP SDK does not expose
/// <c>Icons</c> on its public client types, so we reach into the protocol layer at runtime.
/// Prefers a theme-matched icon when a theme hint is supplied, then falls back to the
/// first icon that has a non-null <c>Source</c>.
/// </summary>
internal static class McpIconHelper
{
    /// <summary>
    /// Returns the URL of the best available icon, or <c>null</c> when none is present.
    /// </summary>
    /// <param name="protocolObject">
    /// The <c>ProtocolTool</c>, <c>ProtocolPrompt</c>, or <c>ProtocolResource</c> instance
    /// retrieved from an <c>McpClientTool</c>, <c>McpClientPrompt</c>, or <c>McpClientResource</c>.
    /// </param>
    /// <param name="theme">
    /// Optional theme hint (e.g. <c>"light"</c> or <c>"dark"</c>). When supplied the first
    /// icon whose <c>Theme</c> property matches (case-insensitive) is preferred.
    /// </param>
    internal static string? GetBestIconUrl(object? protocolObject, string? theme = null)
    {
        if (protocolObject is null) return null;

        try
        {
            var iconsProp = protocolObject.GetType().GetProperty("Icons");
            var iconsValue = iconsProp?.GetValue(protocolObject);
            // Guard: string implements IEnumerable<char> — skip it
            if (iconsValue is string || iconsValue is not System.Collections.IEnumerable icons)
                return null;

            var iconList = icons.Cast<object?>().Where(i => i is not null).ToList();
            if (iconList.Count == 0) return null;

            // Try theme-matched icon first
            if (!string.IsNullOrWhiteSpace(theme))
            {
                foreach (var icon in iconList)
                {
                    var iconTheme = icon!.GetType().GetProperty("Theme")?.GetValue(icon) as string;
                    if (string.Equals(iconTheme, theme, StringComparison.OrdinalIgnoreCase))
                    {
                        var src = ExtractSource(icon);
                        if (src is not null) return src;
                    }
                }
            }

            // Fall back to first icon with a Source
            foreach (var icon in iconList)
            {
                var src = ExtractSource(icon!);
                if (src is not null) return src;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static string? ExtractSource(object icon)
    {
        var sourceProp = icon.GetType().GetProperty("Source");
        var value = sourceProp?.GetValue(icon);
        var url = value switch
        {
            Uri uri => uri.ToString(),
            string s when !string.IsNullOrWhiteSpace(s) => s,
            _ => null,
        };

        // Reject non-http(s) protocols to prevent javascript: URI injection
        if (url is null) return null;
        return url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
               url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            ? url
            : null;
    }
}
