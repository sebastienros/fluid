using System.IO;
using System.Text.Encodings.Web;

namespace Fluid.Ast
{
    public abstract class Statement
    {
        public abstract Completion WriteTo(TextWriter writer, TextEncoder encoder, TemplateContext context);
    }
}
