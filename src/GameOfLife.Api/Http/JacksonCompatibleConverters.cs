using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameOfLife.Api.Http;

/// <summary>
/// A cell value. <c>true</c> and <c>false</c>; an integer, where <c>0</c> is false and any
/// other integer is true; or a string that is <c>true</c>, <c>True</c>, <c>TRUE</c>,
/// <c>false</c>, <c>False</c> or <c>FALSE</c> once surrounding spaces are trimmed. A
/// fraction, any other string, or <c>null</c> is not a cell.
/// </summary>
public sealed class LenientBooleanConverter : JsonConverter<bool>
{
    public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.True:
                return true;
            case JsonTokenType.False:
                return false;
            case JsonTokenType.Number:
                var text = reader.ValueSpan;
                if (text.IndexOfAny("."u8) >= 0 || text.IndexOfAny("eE"u8) >= 0)
                {
                    throw new JsonException("A cell cannot be a fraction.");
                }

                return !text.SequenceEqual("0"u8);
            case JsonTokenType.String:
                return reader.GetString()!.Trim(JavaText.TrimChars) switch
                {
                    "true" or "True" or "TRUE" => true,
                    "false" or "False" or "FALSE" => false,
                    _ => throw new JsonException("A cell string must be true or false."),
                };
            default:
                throw new JsonException("A cell must be a boolean.");
        }
    }

    public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options) =>
        writer.WriteBooleanValue(value);
}

/// <summary>
/// A width or height. An integer; a fraction or exponent, truncated toward zero, that fits an
/// <c>int</c>; or a string holding a decimal integer with an optional sign, once surrounding
/// spaces are trimmed. Anything else is not a dimension.
/// </summary>
public sealed class LenientInt32Converter : JsonConverter<int>
{
    public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Number:
                if (reader.TryGetInt32(out var whole))
                {
                    return whole;
                }

                var text = reader.ValueSpan;
                var isFraction = text.IndexOfAny("."u8) >= 0 || text.IndexOfAny("eE"u8) >= 0;
                if (isFraction && reader.TryGetDouble(out var number)
                    && number is >= int.MinValue and <= int.MaxValue)
                {
                    return (int)Math.Truncate(number);
                }

                throw new JsonException("The number does not fit an int.");
            case JsonTokenType.String:
                var trimmed = reader.GetString()!.Trim(JavaText.TrimChars);
                return int.TryParse(trimmed, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var parsed)
                    ? parsed
                    : throw new JsonException("The string is not an int.");
            default:
                throw new JsonException("The value is not an int.");
        }
    }

    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(value);
}

internal static class JavaText
{
    /// <summary>String.trim(): every character at or below U+0020.</summary>
    public static readonly char[] TrimChars = Enumerable.Range(0, 0x21).Select(i => (char)i).ToArray();
}
