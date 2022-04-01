using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;

namespace Fluid.MvcViewEngine
{
    public class FluidView : IView
    {
        private string _path;
        private FluidRendering _fluidRendering;

        public FluidView(string path, FluidRendering fluidRendering)
        {
            _path = path;
            _fluidRendering = fluidRendering;
        }

        public string Path
        {
            get
            {
                return _path;
            }
        }

        public async Task RenderAsync(ViewContext context)
        {
            await _fluidRendering.RenderAsync(context.Writer, Path, context);
        }
    }
}
