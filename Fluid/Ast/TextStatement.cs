using Microsoft.Extensions.Primitives;
using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Fluid.Ast
{
    public class TextStatement : Statement
    {
        private string _buffer;

        public TextStatement(StringSegment text)
        {
            _buffer = text.ToString();
            Text = text;
        }

        public StringSegment Text { get; }

        public override ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            context.IncrementSteps();

            // The Text fragments are not encoded, but kept as-is
            writer.Write(_buffer);

            return Normal;
        }
    }
}
