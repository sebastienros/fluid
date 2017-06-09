using System.IO;
using System.Text.Encodings.Web;

namespace Fluid.Ast
{
    public class ContinueStatement : Statement
    {
        public override Completion WriteTo(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            return Completion.Continue;
        }
    }
}
