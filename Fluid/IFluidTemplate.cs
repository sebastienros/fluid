using System.Text.Encodings.Web;

namespace Fluid
{
    public interface IFluidTemplate
    {
        ValueTask RenderAsync(TextWriter writer, TextEncoder encoder, TemplateContext context);
    }
}
