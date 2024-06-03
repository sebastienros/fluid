using Microsoft.AspNetCore.Mvc.Rendering;
using System.Threading.Tasks;

namespace Fluid.MvcViewEngine
{
    public class FluidMvcViewOptions
    {
        public delegate ValueTask RenderingMvcViewDelegate(string path, ViewContext viewContext, TemplateContext context);

        /// <summary>
        /// Gets or sets the delegate to execute when a view is rendered.
        /// </summary>
        public RenderingMvcViewDelegate RenderingViewAsync { get; set; }
    }
}
