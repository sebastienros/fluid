using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;

namespace Fluid
{
    // An HTML encoder which passes through all input data. Does no encoding.
    public sealed class NullEncoder : TextEncoder
    {
        private NullEncoder()
        {
        }

        public static NullEncoder Default { get; } = new NullEncoder();

        public override int MaxOutputCharactersPerInputCharacter => 1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override unsafe int FindFirstCharacterToEncode(char* text, int textLength)
        {
            return -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override unsafe bool TryEncodeUnicodeScalar(int unicodeScalar, char* buffer, int bufferLength, out int numberOfCharactersWritten)
        {
            numberOfCharactersWritten = 0;

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool WillEncode(int unicodeScalar)
        {
            return false;
        }
    }
}
