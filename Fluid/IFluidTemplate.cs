using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Fluid.Ast;

namespace Fluid
{
    public interface IFluidTemplate
    {
        IList<Statement> Statements { get; set; }
        Task RenderAsync(TextWriter writer, TextEncoder encoder, TemplateContext context);
    }
}
