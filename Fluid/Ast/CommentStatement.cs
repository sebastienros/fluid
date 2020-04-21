using Microsoft.Extensions.Primitives;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Fluid.Ast
{
    public class CommentStatement : Statement
    {
        public CommentStatement(StringSegment text)
        {
            Text = text.ToString();
        }

        public CommentStatement(string text)
        {
            Text = text;
        }

        public string Text { get; }

        public override ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            context.IncrementSteps();

            return Normal;
        }
    }
}
