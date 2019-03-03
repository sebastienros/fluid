using Microsoft.Extensions.Primitives;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Fluid.Ast
{
    public class TextStatement : Statement
    {
        private char[] _buffer;
        private int _start;
        private int _length;

        public TextStatement(StringSegment text)
        {
            _buffer = text.Buffer.ToCharArray();
            _start = text.Offset;
            _length = text.Length;

            Text = text;
        }

        public StringSegment Text { get; }

        public override ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            // The Text fragments are not encoded, but kept as-is
            writer.Write(_buffer, _start, _length);

            return Normal;
        }
    }
}
