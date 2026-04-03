namespace Garrard.Mcp.MessageContentProtection;

public static class WordValidatorExtensions
{
    /// <summary>
    /// Determines if a word should be treated as sensitive based on simple heuristics.
    /// </summary>
    /// <param name="word">The token to evaluate.</param>
    /// <returns>True if the word is deemed sensitive.</returns>
    public static bool IsSensitive(this string word)
    {
        if (string.IsNullOrWhiteSpace(word)) return false;

        var trimmed = word.Trim();

        if (trimmed.Length == 1) return false;

        if (trimmed.Contains("://", StringComparison.Ordinal)) return false;

        if (IsGenericTypeNotation(trimmed)) return false;

        var core = trimmed.Trim(TokenConstants.SpecialCharacters);

        if (core.Length == 0) return false;

        if (core.All(char.IsLetter)) return false;
        if (core.All(char.IsDigit)) return false;

        if (IsDateLike(core)) return false;
        if (IsTimeLike(core)) return false;
        if (IsOrdinalNumber(core)) return false;
        if (IsContraction(core)) return false;
        if (IsPossessive(core)) return false;
        if (IsHyphenatedWord(core)) return false;
        if (IsSlashSeparatedWords(core)) return false;
        if (IsCommonAbbreviation(core)) return false;
        if (IsVersionPattern(core)) return false;
        if (IsCommonRatioOrPattern(core)) return false;
        if (IsShortAlphanumericCode(core)) return false;
        if (IsMeasurementWithUnit(core)) return false;

        return true;
    }

    private static bool IsDateLike(string core)
    {
        if (string.IsNullOrWhiteSpace(core)) return false;

        char separator = '\0';
        int sepCount = 0;

        foreach (var ch in core)
        {
            if (char.IsDigit(ch)) continue;

            if (ch is '/' or '-' or '.')
            {
                if (separator == '\0')
                    separator = ch;
                else if (separator != ch)
                    return false;

                sepCount++;
                continue;
            }

            return false;
        }

        if (separator == '\0' || sepCount != 2) return false;

        var parts = core.Split(separator);
        if (parts.Length != 3) return false;

        if (!int.TryParse(parts[0], out var first) ||
            !int.TryParse(parts[1], out var second) ||
            !int.TryParse(parts[2], out var third))
            return false;

        bool LooksLikeYear(int value) => value is >= 0 and <= 9999;
        bool IsValidMonth(int value) => value is >= 1 and <= 12;
        bool IsValidDay(int value) => value is >= 1 and <= 31;

        if (LooksLikeYear(first) && IsValidMonth(second) && IsValidDay(third)) return true;
        if (IsValidDay(first) && IsValidMonth(second) && LooksLikeYear(third)) return true;

        return false;
    }

    private static bool IsTimeLike(string core)
    {
        if (string.IsNullOrWhiteSpace(core)) return false;

        int colonCount = 0;
        foreach (var ch in core)
        {
            if (ch == ':') colonCount++;
            else if (!char.IsDigit(ch)) return false;
        }

        if (colonCount is 1 or 2)
        {
            var parts = core.Split(':');
            if (parts.Length is < 2 or > 3) return false;
            if (!int.TryParse(parts[0], out var hour) || hour is < 0 or > 23) return false;
            if (!int.TryParse(parts[1], out var minute) || minute is < 0 or > 59) return false;
            if (parts.Length == 3)
            {
                if (!int.TryParse(parts[2], out var second) || second is < 0 or > 59) return false;
            }
            return true;
        }

        return false;
    }

    private static bool IsOrdinalNumber(string core)
    {
        if (string.IsNullOrWhiteSpace(core)) return false;
        if (core.Length < 3 || core.Length > 6) return false;

        int i = 0;
        while (i < core.Length && char.IsDigit(core[i])) i++;

        if (i == 0 || i == core.Length) return false;

        var suffix = core[i..].ToLowerInvariant();
        if (suffix is not ("st" or "nd" or "rd" or "th")) return false;

        if (!int.TryParse(core[..i], out var number)) return false;
        return number is >= 1 and <= 9999;
    }

    private static bool IsContraction(string core)
    {
        if (string.IsNullOrWhiteSpace(core)) return false;
        if (core.Length is < 3 or > 8) return false;

        var apostropheIndex = core.IndexOf('\'');
        if (apostropheIndex <= 0 || apostropheIndex >= core.Length - 1) return false;

        for (int i = 0; i < core.Length; i++)
        {
            if (i == apostropheIndex) continue;
            if (!char.IsLetter(core[i])) return false;
        }

        return true;
    }

    private static bool IsPossessive(string core)
    {
        if (string.IsNullOrWhiteSpace(core)) return false;
        if (core.Length < 3) return false;

        if (core.EndsWith("'s", StringComparison.Ordinal))
        {
            var stem = core[..^2];
            if (stem.Length > 0 && stem.All(char.IsLetter)) return true;
        }

        if (core.EndsWith("s'", StringComparison.Ordinal))
        {
            var stem = core[..^2];
            if (stem.Length > 0 && stem.All(char.IsLetter)) return true;
        }

        return false;
    }

    private static bool IsHyphenatedWord(string core)
    {
        if (string.IsNullOrWhiteSpace(core)) return false;
        if (!core.Contains('-')) return false;

        var parts = core.Split('-');
        if (parts.Length < 2) return false;

        foreach (var part in parts)
        {
            if (string.IsNullOrEmpty(part)) return false;
            if (!part.All(char.IsLetter)) return false;
        }

        return true;
    }

    private static bool IsSlashSeparatedWords(string core)
    {
        if (string.IsNullOrWhiteSpace(core)) return false;
        if (!core.Contains('/')) return false;

        var parts = core.Split('/');
        if (parts.Length < 2) return false;

        foreach (var part in parts)
        {
            if (string.IsNullOrEmpty(part)) return false;
            if (!part.All(char.IsLetter)) return false;
        }

        return true;
    }

    private static bool IsGenericTypeNotation(string core)
    {
        if (string.IsNullOrWhiteSpace(core)) return false;

        var openIndex = core.IndexOf('<');
        var closeIndex = core.LastIndexOf('>');

        if (openIndex <= 0 || closeIndex < openIndex) return false;

        var typeName = core[..openIndex];
        if (!typeName.All(char.IsLetter)) return false;

        var typeParams = core[(openIndex + 1)..closeIndex];
        if (string.IsNullOrEmpty(typeParams)) return false;

        foreach (var ch in typeParams)
        {
            if (!char.IsLetter(ch) && ch != ',' && ch != ' ') return false;
        }

        return true;
    }

    private static bool IsCommonAbbreviation(string core)
    {
        if (string.IsNullOrWhiteSpace(core)) return false;
        var lower = core.ToLowerInvariant();
        return lower is "e.g" or "i.e" or "eg" or "ie";
    }

    private static bool IsVersionPattern(string core)
    {
        if (string.IsNullOrWhiteSpace(core)) return false;
        if (core.Length < 2) return false;

        var lower = core.ToLowerInvariant();
        if (lower[0] != 'v') return false;

        var rest = lower[1..];
        if (string.IsNullOrEmpty(rest)) return false;

        var parts = rest.Split('.');
        foreach (var part in parts)
        {
            if (string.IsNullOrEmpty(part)) return false;
            if (!part.All(char.IsDigit)) return false;
        }

        return true;
    }

    private static bool IsCommonRatioOrPattern(string core)
    {
        if (string.IsNullOrWhiteSpace(core)) return false;

        if (core.Contains('/') || core.Contains('-'))
        {
            char sep = core.Contains('/') ? '/' : '-';
            var parts = core.Split(sep);
            if (parts.Length == 2 &&
                parts[0].All(char.IsDigit) &&
                parts[1].All(char.IsDigit) &&
                parts[0].Length <= 2 &&
                parts[1].Length <= 2)
                return true;
        }

        return false;
    }

    private static bool IsShortAlphanumericCode(string core)
    {
        if (string.IsNullOrWhiteSpace(core)) return false;
        if (core.Length < 2 || core.Length > 4) return false;
        if (!core.All(c => char.IsLetterOrDigit(c))) return false;

        bool hasLetter = core.Any(char.IsLetter);
        bool hasDigit = core.Any(char.IsDigit);
        if (!hasLetter || !hasDigit) return false;

        if (!core.Where(char.IsLetter).All(char.IsUpper)) return false;

        return true;
    }

    private static bool IsMeasurementWithUnit(string core)
    {
        if (string.IsNullOrWhiteSpace(core)) return false;
        if (core.Length < 2) return false;

        int i = 0;
        while (i < core.Length && char.IsDigit(core[i])) i++;

        if (i == 0 || i == core.Length) return false;

        var numPart = core[..i];
        var unitPart = core[i..].ToLowerInvariant();

        if (numPart.Length > 5) return false;

        string[] knownUnits =
        [
            "mm", "cm", "m", "km", "in", "ft", "yd", "mi",
            "mg", "g", "kg", "lb", "oz",
            "ml", "l", "gal",
            "ms", "s", "min", "h", "hr", "hrs",
            "b", "kb", "mb", "gb", "tb", "pb",
            "x", "px", "pt", "em", "rem", "vw", "vh",
            "c", "f", "k"
        ];

        return knownUnits.Contains(unitPart);
    }
}
