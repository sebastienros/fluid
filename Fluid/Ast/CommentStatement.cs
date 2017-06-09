using System.IO;
using System.Text.Encodings.Web;

namespace Fluid.Ast
{
    public class CommentStatement : Statement
    {
        public CommentStatement(string text)
        {
            Text = text;
        }

        public string Text { get; }

        public override Completion WriteTo(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            return Completion.Normal;
        }
    }
}
