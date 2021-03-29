using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Fluid
{
    public interface IFluidTemplate
    {
        ValueTask RenderAsync(TextWriter writer, TextEncoder encoder, TemplateContext context);
    }
}
