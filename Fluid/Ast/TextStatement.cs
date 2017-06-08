using System.IO;
using System.Text.Encodings.Web;

namespace Fluid.Ast
{
    public class TextStatement : Statement
    {
        public TextStatement(string text)
        {
            Text = text;
        }

        public string Text { get; }

        public override Completion WriteTo(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            writer.Write(Text, encoder);

            return Completion.Normal;
        }
    }
}
