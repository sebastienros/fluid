using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace Fluid.Utils;

/// <summary>
/// Orders strings like Liquid: Ruby compares strings by their UTF-8 bytes (<c>String#&lt;=&gt;</c>), which is
/// code point order, and <c>sort_natural</c> compares their lowercase forms the same way.
/// </summary>
/// <remarks>
/// An ordinal comparison of UTF-16 code units is already code point order, except when a surrogate
/// (<c>U+D800-U+DFFF</c>, half of a character outside the BMP) is compared with a char in <c>U+E000-U+FFFF</c>.
/// Only the first differing char is checked for that, so ASCII strings cost an ordinal comparison.
/// </remarks>
public static class LiquidStringComparer
{
    /// <summary>
    /// Compares two strings like Ruby's <c>String#&lt;=&gt;</c>, in code point order (UTF-8 byte order).
    /// </summary>
    public static int Compare(string x, string y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x is null || y is null)
        {
            return x is null ? -1 : 1;
        }

        var i = CommonPrefixLength(x, y);

        if (i == x.Length || i == y.Length)
        {
            return x.Length - y.Length;
        }

        return CompareCodeUnits(x[i], y[i]);
    }

    /// <summary>
    /// Compares two strings like <c>sort_natural</c> does in Shopify: as if both were lowercased
    /// (Ruby's <c>String#downcase</c>), then compared in code point order.
    /// </summary>
    /// <remarks>
    /// Letters are folded to lowercase, not to uppercase, so the characters between <c>Z</c> and <c>a</c> come
    /// before the letters. Letters outside ASCII are folded too, including the ones outside the BMP, except
    /// titlecase letters (<c>U+01C5</c>), which Ruby leaves as they are. Unlike Ruby, <c>U+0130</c> isn't
    /// expanded to two characters.
    /// </remarks>
    public static int CompareIgnoreCase(string x, string y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x is null || y is null)
        {
            return x is null ? -1 : 1;
        }

        var length = Math.Min(x.Length, y.Length);

        for (var i = CommonPrefixLength(x, y); i < length; i++)
        {
            var cx = x[i];
            var cy = y[i];

            if (cx == cy)
            {
                continue;
            }

            if ((cx | cy) < 0x80)
            {
                cx = ToLowerAscii(cx);
                cy = ToLowerAscii(cy);

                if (cx != cy)
                {
                    return cx - cy;
                }

                continue;
            }

            // The chars before i are the same, or the same letters: when i is the second half of a surrogate
            // pair, both strings have the same first half.
            var start = i > 0 && char.IsHighSurrogate(x[i - 1]) && (char.IsLowSurrogate(cx) || char.IsLowSurrogate(cy)) ? i - 1 : i;

            var px = ToLowerCodePoint(x, start);
            var py = ToLowerCodePoint(y, start);

            if (px != py)
            {
                return px - py;
            }
        }

        return x.Length - y.Length;
    }

    /// <summary>
    /// Returns the lowercase code point of the character that starts at an index.
    /// </summary>
    private static int ToLowerCodePoint(string s, int index)
    {
        var c = s[index];

        if (!char.IsSurrogate(c))
        {
            if (c < 0x80)
            {
                return ToLowerAscii(c);
            }

            // Like Ruby's downcase, titlecase letters are not changed.
            return char.GetUnicodeCategory(c) == UnicodeCategory.TitlecaseLetter ? c : char.ToLowerInvariant(c);
        }

        if (!char.IsHighSurrogate(c) || index + 1 >= s.Length || !char.IsLowSurrogate(s[index + 1]))
        {
            // Half of a surrogate pair
            return c;
        }

#if NET8_0_OR_GREATER
        return Rune.ToLowerInvariant(new Rune(c, s[index + 1])).Value;
#else
        return char.ConvertToUtf32(s.Substring(index, 2).ToLowerInvariant(), 0);
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CommonPrefixLength(string x, string y)
    {
        // Sorted strings often differ at their first char, where a vectorized search costs more than it saves.
        if (x.Length == 0 || y.Length == 0 || x[0] != y[0])
        {
            return 0;
        }

#if NET8_0_OR_GREATER
        return x.AsSpan(1).CommonPrefixLength(y.AsSpan(1)) + 1;
#else
        var length = Math.Min(x.Length, y.Length);
        var i = 1;

        while (i < length && x[i] == y[i])
        {
            i++;
        }

        return i;
#endif
    }

    private static char ToLowerAscii(char c) => (uint)(c - 'A') <= 'Z' - 'A' ? (char)(c | 0x20) : c;

    /// <summary>
    /// Compares the first differing code units of two strings in code point order.
    /// </summary>
    private static int CompareCodeUnits(char x, char y)
    {
        // Below U+D800 the order is the same. Above, surrogates (U+D800-U+DFFF) are moved after U+E000-U+FFFF,
        // since they encode the code points from U+10000. Two surrogates keep their order.
        if (x >= '\uD800' && y >= '\uD800')
        {
            return FixUp(x) - FixUp(y);
        }

        return x - y;
    }

    private static int FixUp(char c) => c >= '\uE000' ? c - 0x800 : c + 0x2000;
}
