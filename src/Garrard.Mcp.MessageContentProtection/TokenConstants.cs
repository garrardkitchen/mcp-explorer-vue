namespace Garrard.Mcp.MessageContentProtection;

/// <summary>
/// Shared constants for token processing.
/// </summary>
public static class TokenConstants
{
    /// <summary>
    /// Special characters that may wrap tokens and should be trimmed during token processing.
    /// </summary>
    public static readonly char[] SpecialCharacters =
    [
        '£', '@', '#', '$', '%', '^', '&', '*', '(', ')', '-', '_', '=', '+', '{', '}', '[', ']', '|', '\\',
        '/', '<', '>', '?', '~', '`', '.', ';', '!', ':', ',', '"', '\'',
    ];
}
