namespace Fluid.Utils;

/// <summary>
/// Lengths and indices in code points, like Ruby's strings, which Liquid uses: a character outside the BMP
/// (an emoji) counts as one, not as the two chars of its surrogate pair.
/// </summary>
internal static class CodePoints
{
    /// <summary>
    /// Whether the string has surrogates, the only case where code points and chars differ.
    /// </summary>
    public static bool HasSurrogates(ReadOnlySpan<char> value)
    {
#if NET8_0_OR_GREATER
        return value.IndexOfAnyInRange('\uD800', '\uDFFF') >= 0;
#else
        foreach (var c in value)
        {
            if (char.IsSurrogate(c))
            {
                return true;
            }
        }

        return false;
#endif
    }

    /// <summary>
    /// The number of code points of a string with surrogates. A lone surrogate counts as one.
    /// </summary>
    public static int Count(string value)
    {
        var count = 0;

        for (var i = 0; i < value.Length; i += Width(value, i))
        {
            count++;
        }

        return count;
    }

    /// <summary>
    /// The char index of the code point at <paramref name="index"/>, or the length of the string past its end.
    /// </summary>
    public static int ToCharIndex(string value, int index)
    {
        var i = 0;

        while (index > 0 && i < value.Length)
        {
            i += Width(value, i);
            index--;
        }

        return i;
    }

    /// <summary>
    /// The number of chars of the code point at <paramref name="i"/>: 2 for a surrogate pair, 1 otherwise.
    /// </summary>
    public static int Width(string value, int i)
    {
        return char.IsHighSurrogate(value[i]) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]) ? 2 : 1;
    }
}
