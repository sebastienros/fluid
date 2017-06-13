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

        public override async Task<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            await writer.WriteAsync(Text, encoder);

            return Completion.Normal;
        }
    }
}
