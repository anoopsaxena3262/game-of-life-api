using System.Globalization;

namespace GameOfLife.Api.Http;

/// <summary>
/// The rules the reference service applies when it turns a path or query string into an
/// <c>int</c> or a UUID. Reproduced here so the same request gets the same answer.
/// </summary>
public static class SpringConversions
{
    /// <summary>
    /// A path or query value as an <c>int</c>. Every whitespace character is removed first,
    /// anywhere in the value. A <c>0x</c>, <c>0X</c> or <c>#</c> prefix, with an optional
    /// leading minus, is hexadecimal; otherwise the value is decimal with an optional sign.
    /// </summary>
    /// <returns>The number, or null when the value is empty after whitespace is removed.</returns>
    /// <exception cref="FormatException">The value is not an <c>int</c>.</exception>
    public static int? ParseInt(string value)
    {
        var compact = string.Concat(value.Where(c => !IsJavaWhitespace(c)));
        if (compact.Length == 0)
        {
            return null;
        }

        var negative = compact[0] == '-';
        var digitsStart = negative ? 1 : 0;
        string? hex = null;
        if (compact.AsSpan(digitsStart).StartsWith("0x") || compact.AsSpan(digitsStart).StartsWith("0X"))
        {
            hex = compact[(digitsStart + 2)..];
        }
        else if (compact.AsSpan(digitsStart).StartsWith("#"))
        {
            hex = compact[(digitsStart + 1)..];
        }

        if (hex is null)
        {
            return int.TryParse(compact, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : throw new FormatException(value);
        }

        // Hexadecimal: no second sign, and the signed result must fit an int.
        if (hex.Length == 0 || hex[0] is '-' or '+'
            || !long.TryParse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var magnitude)
            || magnitude < 0)
        {
            throw new FormatException(value);
        }

        var signed = negative ? -magnitude : magnitude;
        return signed is >= int.MinValue and <= int.MaxValue ? (int)signed : throw new FormatException(value);
    }

    /// <summary>
    /// A path value as a UUID. Leading and trailing control characters and spaces are trimmed.
    /// The value is then five hexadecimal fields separated by dashes, at most 36 characters in
    /// all. Short fields are allowed (<c>1-1-1-1-1</c>), each field may start with <c>+</c>,
    /// and a field longer than its slot keeps only its low digits.
    /// </summary>
    /// <exception cref="FormatException">The value is not a UUID.</exception>
    public static Guid ParseUuid(string value)
    {
        var name = value.Trim(JavaText.TrimChars);
        if (name.Length is 0 or > 36)
        {
            throw new FormatException(value);
        }

        var dash1 = name.IndexOf('-');
        var dash2 = name.IndexOf('-', dash1 + 1);
        var dash3 = dash2 < 0 ? -1 : name.IndexOf('-', dash2 + 1);
        var dash4 = dash3 < 0 ? -1 : name.IndexOf('-', dash3 + 1);
        var dash5 = dash4 < 0 ? -1 : name.IndexOf('-', dash4 + 1);
        if (dash1 < 0 || dash4 < 0 || dash5 >= 0)
        {
            throw new FormatException(value);
        }

        ulong mostSignificant = HexField(name, 0, dash1, value) & 0xffffffffUL;
        mostSignificant = (mostSignificant << 16) | (HexField(name, dash1 + 1, dash2, value) & 0xffffUL);
        mostSignificant = (mostSignificant << 16) | (HexField(name, dash2 + 1, dash3, value) & 0xffffUL);
        ulong leastSignificant = HexField(name, dash3 + 1, dash4, value) & 0xffffUL;
        leastSignificant = (leastSignificant << 48) | (HexField(name, dash4 + 1, name.Length, value) & 0xffffffffffffUL);

        return Guid.ParseExact($"{mostSignificant:x16}{leastSignificant:x16}", "N");
    }

    // A signed 64-bit hexadecimal field. A leading '+' is allowed; the magnitude must fit a
    // signed long. Digits are any decimal digit or a Latin or full-width letter a-f.
    private static ulong HexField(string name, int start, int end, string original)
    {
        var span = name.AsSpan(start, end - start);
        if (span.Length > 0 && span[0] == '+')
        {
            span = span[1..];
        }

        if (span.IsEmpty)
        {
            throw new FormatException(original);
        }

        ulong result = 0;
        foreach (var c in span)
        {
            var digit = HexDigit(c);
            if (digit < 0 || result > (long.MaxValue - (ulong)digit) / 16)
            {
                throw new FormatException(original);
            }

            result = (result * 16) + (ulong)digit;
        }

        return result;
    }

    private static int HexDigit(char c)
    {
        var decimalDigit = CharUnicodeInfo.GetDecimalDigitValue(c);
        if (decimalDigit >= 0)
        {
            return decimalDigit;
        }

        return c switch
        {
            >= 'a' and <= 'f' => c - 'a' + 10,
            >= 'A' and <= 'F' => c - 'A' + 10,
            >= 'ａ' and <= 'ｆ' => c - 'ａ' + 10,
            >= 'Ａ' and <= 'Ｆ' => c - 'Ａ' + 10,
            _ => -1,
        };
    }

    // Character.isWhitespace(): Unicode space separators except the non-breaking ones, line
    // and paragraph separators, and the ASCII control characters 9-13 and 28-31.
    private static bool IsJavaWhitespace(char c)
    {
        if (c is ' ' or ' ' or ' ')
        {
            return false;
        }

        return c is (>= '\t' and <= '\r') or (>= '\u001c' and <= '\u001f')
            || CharUnicodeInfo.GetUnicodeCategory(c) is UnicodeCategory.SpaceSeparator
                or UnicodeCategory.LineSeparator
                or UnicodeCategory.ParagraphSeparator;
    }
}
