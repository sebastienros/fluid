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
            Text = text;
        }

        public StringSegment Text { get; }

        public override ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            return Normal;
        }
    }
}
