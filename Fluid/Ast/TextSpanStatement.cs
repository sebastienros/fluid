using Parlot.Fluent;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Fluid.Ast
{
    public class TextSpanStatement : Statement
    {
        public TextSpanStatement(TextSpan text)
        {
            Text = text;
        }

        public TextSpanStatement(string text)
        {
            Text = new TextSpan(text);
        }

        public TextSpan Text { get; }

        public override ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            context.IncrementSteps();

            // The Text fragments are not encoded, but kept as-is
            writer.Write(Text.Span);

            return Normal;
        }
    }
}
