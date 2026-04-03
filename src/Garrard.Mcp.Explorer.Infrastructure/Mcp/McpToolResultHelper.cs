using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;

namespace Garrard.Mcp.Explorer.Infrastructure.Mcp;

/// <summary>
/// Converts a raw MCP <see cref="CallToolResult"/> into clean, indented JSON suitable
/// for storage and display. Handles StructuredContent, TextContentBlock, and ImageContentBlock.
/// Always propagates <c>isError</c> when the tool call reported a failure.
/// </summary>
internal static class McpToolResultHelper
{
    private static readonly JsonSerializerOptions Opts = new() { WriteIndented = true };

    public static string ConvertToJson(CallToolResult toolResult)
    {
        var isError = toolResult.IsError == true;

        // Use StructuredContent when present (it is already a parsed JSON value)
        if (toolResult.StructuredContent is not null)
        {
            if (!isError)
                return JsonSerializer.Serialize(toolResult.StructuredContent.Value, Opts);

            // Merge isError into the serialised object so callers always see the flag
            var raw = JsonSerializer.Serialize(toolResult.StructuredContent.Value, Opts);
            try
            {
                var node = JsonNode.Parse(raw);
                if (node is JsonObject obj)
                {
                    obj["isError"] = true;
                    return obj.ToJsonString(Opts);
                }
            }
            catch { /* fall through to wrapped form */ }
            return JsonSerializer.Serialize(new { value = toolResult.StructuredContent.Value, isError = true }, Opts);
        }

        // Collect text from Content blocks
        var textParts = new List<string>();
        if (toolResult.Content is { Count: > 0 })
        {
            foreach (var block in toolResult.Content)
            {
                textParts.Add(block switch
                {
                    TextContentBlock textBlock => textBlock.Text ?? string.Empty,
                    ImageContentBlock imageBlock => $"[Image: {imageBlock.MimeType}]",
                    _ => block.ToString() ?? string.Empty
                });
            }
        }

        if (textParts.Count > 0)
        {
            var combinedText = string.Join("\n", textParts);
            try
            {
                var parsed = JsonNode.Parse(combinedText);
                if (parsed is not null)
                {
                    if (!isError)
                        return parsed.ToJsonString(Opts);

                    // Inject isError into the parsed JSON object if possible
                    if (parsed is JsonObject parsedObj)
                    {
                        parsedObj["isError"] = true;
                        return parsedObj.ToJsonString(Opts);
                    }

                    // Parsed as array or scalar — wrap it
                    return JsonSerializer.Serialize(new { value = JsonNode.Parse(combinedText), isError = true }, Opts);
                }
            }
            catch { /* Not JSON — wrap as text below */ }

            return isError
                ? JsonSerializer.Serialize(new { text = combinedText, isError = true }, Opts)
                : JsonSerializer.Serialize(new { text = combinedText }, Opts);
        }

        return isError
            ? JsonSerializer.Serialize(new { isError = true }, Opts)
            : JsonSerializer.Serialize(new { }, Opts);
    }
}
