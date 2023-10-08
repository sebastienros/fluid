using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Fluid.Ast;

namespace Fluid
{
    public interface IFluidTemplate
    {
        ValueTask RenderAsync(TextWriter writer, TextEncoder encoder, TemplateContext context);
    }
}
