using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Fluid.Ast
{
    public class TextStatement : Statement
    {
        public TextStatement(string text)
        {
            Text = text;
        }

        public string Text { get; }

        public override Task<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            // The Text fragments are not encoded, but kept as-is
            writer.Write(Text);

            return Task.FromResult(Completion.Normal);
        }
    }
}
