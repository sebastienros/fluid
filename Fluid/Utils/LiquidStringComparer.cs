using System.Runtime.CompilerServices;

namespace Fluid.Utils;

/// <summary>
/// Orders strings like Liquid: Ruby compares strings by their UTF-8 bytes (<c>String#&lt;=&gt;</c>), which is
/// code point order, and <c>sort_natural</c> uses <c>String#casecmp</c>, which also folds <c>A-Z</c> to
/// <c>a-z</c>, and no other letter.
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
    /// Compares two strings like Ruby's <c>String#casecmp</c>, which <c>sort_natural</c> uses: <c>A-Z</c> are
    /// folded to <c>a-z</c>, other letters are compared as they are, in code point order.
    /// </summary>
    public static int CompareIgnoreAsciiCase(string x, string y)
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

            cx = ToLowerAscii(cx);
            cy = ToLowerAscii(cy);

            if (cx != cy)
            {
                return CompareCodeUnits(cx, cy);
            }
        }

        return x.Length - y.Length;
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

    private static int FixUp(char c) => c >= '' ? c - 0x800 : c + 0x2000;
}
