using Microsoft.Extensions.Primitives;
using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Fluid.Ast
{
    public class CommentStatement : Statement
    {
        private char[] _buffer;

        public CommentStatement(StringSegment text)
        {
            _buffer = new char[text.Length];
            text.Buffer.CopyTo(text.Offset, _buffer, 0, text.Length);
        }

        public CommentStatement(string text)
        {
            _buffer = text.ToCharArray();
        }

        public string Text => new String(_buffer);

        public override ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            context.IncrementSteps();

            return Normal;
        }
    }
}
