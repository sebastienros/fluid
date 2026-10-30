using System.Runtime.CompilerServices;
using Parlot;
using Parlot.Fluent;

namespace Fluid.Parser;

/// <summary>
/// The whitespace of Liquid: space, <c>\t</c>, <c>\n</c>, <c>\v</c>, <c>\f</c> and <c>\r</c>, the chars of Ruby's <c>\s</c>.
/// They are the only ones Shopify skips in tags and outputs, trims, or reads as a blank text: it reads U+00A0, U+2028 or
/// U+3000 like any other char. <see cref="FluidParseContext"/> selects them for the parsers with <see cref="Set"/>.
/// </summary>
internal static class LiquidWhiteSpace
{
    public const WhiteSpaceSet Set = WhiteSpaceSet.Ascii;

    /// <summary>
    /// Skips any whitespace without reading the end of a line of a <c>{% liquid %}</c> tag, and always succeeds.
    /// Shopify skips <c>\v</c> and <c>\r</c> before the new line, which Parlot reads as new lines.
    /// </summary>
    public static readonly Parser<TextSpan> SkipLine = Literals.AnyOf(" \t\v\f\r".AsSpan(), minSize: 0);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsWhiteSpace(char c) => Character.IsWhiteSpaceOrNewLine(c, Set);

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
}
