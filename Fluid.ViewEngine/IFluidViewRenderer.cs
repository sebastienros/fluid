using System.IO;
using System.Threading.Tasks;

namespace Fluid.ViewEngine
{
    public interface IFluidViewRenderer
    {
        Task RenderViewAsync(TextWriter writer, string path, TemplateContext context);
        Task RenderPartialAsync(TextWriter writer, string path, TemplateContext context);
    }
}
