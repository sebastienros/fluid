#if NET6_0_OR_GREATER

namespace Fluid.Utils;

public class HexUtilities
{
#if !NET9_0_OR_GREATER
    // The longest value that is converted on the stack, which fits a SHA-512 hash.
    private const int MaxStackallocBytes = 64;

    private static ReadOnlySpan<byte> HexChars => "0123456789abcdef"u8;
#endif

    public static string ToHexLower(ReadOnlySpan<byte> bytes)
    {
#if NET9_0_OR_GREATER
        return Convert.ToHexStringLower(bytes);
#else
        Span<char> chars = bytes.Length <= MaxStackallocBytes
            ? stackalloc char[MaxStackallocBytes * 2]
            : new char[bytes.Length * 2];

        var j = 0;
        for (var i = 0; i < bytes.Length; i++)
        {
            var b = bytes[i];
            chars[j++] = (char)HexChars[(b >> 4) & 0x0f];
            chars[j++] = (char)HexChars[b & 0x0f];
        }

        return new string(chars.Slice(0, j));
#endif
    }
}
#endif
