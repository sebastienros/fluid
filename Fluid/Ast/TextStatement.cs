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
            writer.Write(Text, encoder);

            return Task.FromResult(Completion.Normal);
        }
    }
}
