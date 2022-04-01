using Fluid.ViewEngine;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Threading.Tasks;

namespace Fluid.MvcViewEngine
{
    public class FluidMvcViewOptions : FluidViewEngineOptions
    {
        public delegate ValueTask RenderingMvcViewDelegate(string path, ViewContext viewContext, TemplateContext context);

        /// <summary>
        /// Gets or sets the delegate to execute when a view is rendered.
        /// </summary>
        public new RenderingMvcViewDelegate RenderingViewAsync { get; set; }
    }
}
