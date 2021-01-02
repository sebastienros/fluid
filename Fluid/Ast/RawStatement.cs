using Parlot.Fluent;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Fluid.Ast
{
    public class RawStatement : Statement
    {
        public RawStatement(TextSpan text)
        {
            Text = text;
        }

        public TextSpan Text { get; }

        public override ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            context.IncrementSteps();

            return Normal;
        }
    }
}
