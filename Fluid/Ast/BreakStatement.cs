using System.IO;
using System.Text.Encodings.Web;

namespace Fluid.Ast
{
    public class BreakStatement : Statement
    {
        public override Completion WriteTo(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            return Completion.Break;
        }
    }
}
