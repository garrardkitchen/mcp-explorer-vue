using System.Text;
using Garrard.Mcp.Explorer.Infrastructure;

namespace Garrard.Tests.Mcp.Explorer.Infrastructure;

/// <summary>Tests for <see cref="Utf8Text.SafeLength"/>.</summary>
public class Utf8TextTests
{
    [Fact]
    public void SafeLength_BodyWithinLimit_ReturnsFullLength()
    {
        var bytes = Encoding.UTF8.GetBytes("hello");

        Assert.Equal(bytes.Length, Utf8Text.SafeLength(bytes, 100));
    }

    [Fact]
    public void SafeLength_BodyWithinLimitEndingWithMultiByteChar_IsNotTrimmed()
    {
        // Regression: a complete body ending in a multi-byte character must not lose it.
        var bytes = Encoding.UTF8.GetBytes("café");

        var length = Utf8Text.SafeLength(bytes, 100);

        Assert.Equal(bytes.Length, length);
        Assert.Equal("café", Encoding.UTF8.GetString(bytes, 0, length));
    }

    [Fact]
    public void SafeLength_TruncationMidCharacter_BacksUpToCharacterBoundary()
    {
        // "aé" = 0x61 0xC3 0xA9 — a 2-byte limit slices the é in half.
        var bytes = Encoding.UTF8.GetBytes("aé");

        var length = Utf8Text.SafeLength(bytes, 2);

        Assert.Equal(1, length);
        Assert.Equal("a", Encoding.UTF8.GetString(bytes, 0, length));
    }

    [Fact]
    public void SafeLength_TruncationOnCharacterBoundary_KeepsWholePrefix()
    {
        // "éa" = 0xC3 0xA9 0x61 — a 2-byte limit lands exactly after the é.
        var bytes = Encoding.UTF8.GetBytes("éa");

        var length = Utf8Text.SafeLength(bytes, 2);

        Assert.Equal(2, length);
        Assert.Equal("é", Encoding.UTF8.GetString(bytes, 0, length));
    }

    [Fact]
    public void SafeLength_FourByteCharacterSliced_ExcludesIt()
    {
        // "a😀" = 0x61 + 4-byte emoji; limits 2–4 all slice the emoji.
        var bytes = Encoding.UTF8.GetBytes("a😀");

        for (var limit = 2; limit <= 4; limit++)
        {
            Assert.Equal(1, Utf8Text.SafeLength(bytes, limit));
        }
    }

    [Fact]
    public void SafeLength_EmptyInput_ReturnsZero()
    {
        Assert.Equal(0, Utf8Text.SafeLength([], 10));
    }
}
