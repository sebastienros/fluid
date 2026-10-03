using System.Text.Encodings.Web;

namespace Fluid
{
    public interface IFluidTemplate
    {
        ValueTask RenderAsync(IFluidOutput output, TextEncoder encoder, TemplateContext context);
    }
}
