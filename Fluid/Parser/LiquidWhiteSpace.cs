using System.Runtime.CompilerServices;
using Parlot;
using Parlot.Fluent;

namespace Fluid.Parser;

/// <summary>
/// The whitespace of Liquid: space, <c>\t</c>, <c>\n</c>, <c>\v</c>, <c>\f</c> and <c>\r</c>, the chars of Ruby's <c>\s</c>.
/// They are the only ones Shopify skips in tags and outputs, trims, or reads as a blank text: it reads U+00A0, U+2028 or
/// U+3000 like any other char. Parlot's own set has the Unicode spaces, so <see cref="FluidParseContext"/> skips
/// whitespace with <see cref="Skip"/>.
/// </summary>
internal static class LiquidWhiteSpace
{
    public const string Chars = " \t\n\v\f\r";

    /// <summary>
    /// The whitespace which doesn't end a line of a <c>{% liquid %}</c> tag.
    /// </summary>
    public const string LineChars = " \t\v\f\r";

    /// <summary>
    /// Skips any whitespace, and always succeeds.
    /// </summary>
    public static readonly Parser<TextSpan> Skip = new WhiteSpaceSkipper();

    /// <summary>
    /// Reads some whitespace.
    /// </summary>
    public static readonly Parser<TextSpan> Required = Literals.AnyOf(Chars.AsSpan());

    /// <summary>
    /// Skips any whitespace without reading the end of the line, and always succeeds.
    /// </summary>
    public static readonly Parser<TextSpan> SkipLine = Literals.AnyOf(LineChars.AsSpan(), minSize: 0);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsWhiteSpace(char c) => c == ' ' || (uint)(c - '\t') <= '\r' - '\t';

    /// <summary>
    /// Whether the whitespace control removes the char: Ruby's strip removes NUL too.
    /// </summary>
    public static bool IsTrimmed(char c) => IsWhiteSpace(c) || c == '\0';

    public static bool IsBlank(ReadOnlySpan<char> text)
    {
        foreach (var c in text)
        {
            if (!IsWhiteSpace(c))
            {
                return false;
            }
        }

        return true;
    }

    private sealed class WhiteSpaceSkipper : Parser<TextSpan>
    {
        public override bool Parse(ParseContext context, ref ParseResult<TextSpan> result)
        {
            var cursor = context.Scanner.Cursor;

            if (!IsWhiteSpace(cursor.Current))
            {
                return true;
            }

            var span = cursor.Span;
            var length = 1;

            while (length < span.Length && IsWhiteSpace(span[length]))
            {
                length++;
            }

            var start = cursor.Offset;
            cursor.Advance(length);
            result.Set(start, start + length, cursor.CreateSpan(start, length));
            return true;
        }
    }
}
