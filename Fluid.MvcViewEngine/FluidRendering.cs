using Fluid.ViewEngine;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Options;
using System.IO;
using System.Threading.Tasks;

namespace Fluid.MvcViewEngine
{
    /// <summary>
    /// This class is registered as a singleton.
    /// </summary>
    public class FluidRendering
    {
        private readonly IFluidViewRenderer _fluidViewRenderer;
        private readonly IWebHostEnvironment _hostingEnvironment;
        private readonly FluidViewEngineOptions _options;
        private readonly FluidMvcViewOptions _mvcOptions;

        public FluidRendering(
            IFluidViewRenderer fluidViewRenderer,
            IOptions<FluidViewEngineOptions> optionsAccessor,
            IOptions<FluidMvcViewOptions> mvcOptionsAccessor,
            IWebHostEnvironment hostingEnvironment)
        {
            _hostingEnvironment = hostingEnvironment;
            _options = optionsAccessor.Value;
            _mvcOptions = mvcOptionsAccessor.Value;
            _fluidViewRenderer = fluidViewRenderer;
        }

        public async Task RenderAsync(TextWriter writer, string path, ViewContext viewContext)
        {
            var context = new TemplateContext(_options.TemplateOptions);
            context.SetValue("ViewData", viewContext.ViewData);
            context.SetValue("ModelState", viewContext.ModelState);
            context.SetValue("Model", viewContext.ViewData.Model);

            if (_mvcOptions.RenderingViewAsync != null)
            {
                await _mvcOptions.RenderingViewAsync.Invoke(path, viewContext, context);
            }

            await _fluidViewRenderer.RenderViewAsync(writer, path, context);
        }
    }
}
