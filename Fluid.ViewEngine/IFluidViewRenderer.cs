using System.IO;
using System.Threading.Tasks;

namespace Fluid.ViewEngine
{
    public interface IFluidViewRenderer
    {
        Task RenderViewAsync(IFluidOutput output, string path, TemplateContext context);
        Task RenderPartialAsync(IFluidOutput output, string path, TemplateContext context);
    }
}
