#if NET6_0_OR_GREATER

namespace Fluid.Utils;

public class HexUtilities
{
    private static ReadOnlySpan<byte> HexChars => "0123456789abcdef"u8;

    public static string ToHexLower(byte[] hash)
    {
        return string.Create(hash.Length * 2, hash, (span, hash) =>
        {
            var j = 0;
            var length = hash.Length;
            for (var i = 0; i < length; i++)
            {
                var b = hash[i];
                span[j++] = (char)HexChars[(b >> 4) & 0x0f];
                span[j++] = (char)HexChars[b & 0x0f];
            }
        });
    }
}
#endif