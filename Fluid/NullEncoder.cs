using System;
using System.IO;
using System.Text.Encodings.Web;

namespace Fluid
{
    // An HTML encoder which passes through all input data. Does no encoding.
    // Copied from Microsoft.AspNetCore.Razor.TagHelpers.NullHtmlEncoder.
    public class NullEncoder : HtmlEncoder
    {
        private NullEncoder()
        {
        }

        public static new NullEncoder Default { get; } = new NullEncoder();

        public override int MaxOutputCharactersPerInputCharacter => 1;

        public override string Encode(string value)
        {
            return value;
        }

        public override void Encode(TextWriter output, char[] value, int startIndex, int characterCount)
        {
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            if (characterCount == 0)
            {
                return;
            }

            output.Write(value, startIndex, characterCount);
        }

        public override void Encode(TextWriter output, string value, int startIndex, int characterCount)
        {
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (characterCount == 0)
            {
                return;
            }

            output.Write(value.Substring(startIndex, characterCount));
        }

        public override unsafe int FindFirstCharacterToEncode(char* text, int textLength)
        {
            return -1;
        }

        public override unsafe bool TryEncodeUnicodeScalar(
            int unicodeScalar,
            char* buffer,
            int bufferLength,
            out int numberOfCharactersWritten)
        {
            numberOfCharactersWritten = 0;

            return false;
        }

        public override bool WillEncode(int unicodeScalar)
        {
            return false;
        }
    }
}
