using Microsoft.Extensions.Primitives;
using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Fluid.Ast
{
    public class TextStatement : Statement
    {

#if !NETSTANDARD2_1
        private string _buffer;
#endif

        public TextStatement(StringSegment text)
        {
            Text = text;
        }

        public StringSegment Text { get; }

        public override ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            context.IncrementSteps();


            // The Text fragments are not encoded, but kept as-is
#if NETSTANDARD2_1
            writer.Write(Text.Buffer.AsSpan(Text.Offset, Text.Length));
#else
            _buffer ??= Text.ToString();
            writer.Write(_buffer);
#endif

            return Normal;
        }
    }
}
