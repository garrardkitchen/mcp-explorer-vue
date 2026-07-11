namespace Garrard.Mcp.Explorer.Infrastructure;

public static class Utf8Text
{
    /// <summary>
    /// Returns the largest byte count ≤ <paramref name="maxBytes"/> that ends on a complete UTF-8 character boundary,
    /// avoiding replacement characters from slicing mid-sequence. Bodies that already fit within the limit are
    /// returned whole — trailing multi-byte characters are only trimmed when the input was actually truncated.
    /// </summary>
    public static int SafeLength(byte[] bytes, int maxBytes)
    {
        if (bytes.Length <= maxBytes) return bytes.Length;

        // Walk back past continuation bytes (10xxxxxx) to the lead byte of the sequence
        // that straddles — or ends exactly at — the cut point.
        var start = maxBytes;
        while (start > 0 && (bytes[start - 1] & 0xC0) == 0x80) start--;
        if (start == 0) return maxBytes; // no lead byte in range (malformed UTF-8) — cut as-is

        var lead = bytes[start - 1];
        var sequenceLength =
            (lead & 0x80) == 0x00 ? 1 :
            (lead & 0xE0) == 0xC0 ? 2 :
            (lead & 0xF0) == 0xE0 ? 3 :
            (lead & 0xF8) == 0xF0 ? 4 : 1;

        // Keep the sequence when it fits entirely within the limit; otherwise cut before it.
        return start - 1 + sequenceLength <= maxBytes ? maxBytes : start - 1;
    }
}
