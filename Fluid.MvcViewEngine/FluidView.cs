using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;

namespace FluidMvcViewEngine
{
    public class FluidView : IView
    {
        private string _path;
        private IFluidRendering _fluidRendering;

        public FluidView(string path, IFluidRendering fluidRendering)
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
            var result = await _fluidRendering.RenderAsync(Path, context.ViewData.Model, context.ViewData, context.ModelState);
            context.Writer.Write(result);
        }
    }
}
