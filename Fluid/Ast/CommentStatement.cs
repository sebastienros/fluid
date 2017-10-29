using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Fluid.Ast
{
    public class CommentStatement : Statement
    {
        public CommentStatement(string text)
        {
            Text = text;
        }

        public string Text { get; }

        public override Task<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            context.LocalScope.SetValue("IsComment", true);
            return Task.FromResult(Completion.Normal);
        }
    }
}
