using Fluid.ViewEngine;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.FileProviders;
using System.Threading.Tasks;

namespace Fluid.MvcViewEngine
{
    public class FluidMvcViewOptions : FluidViewEngineOptions
    {
        /// <summary>
        /// Gets or sets the synchronous provider used by ASP.NET Core to locate named views.
        /// </summary>
        public IFileProvider ViewLocationFileProvider { get; set; }

        public delegate ValueTask RenderingMvcViewDelegate(string path, ViewContext viewContext, TemplateContext context);

        /// <summary>
        /// Gets or sets the delegate to execute when a view is rendered.
        /// </summary>
        public new RenderingMvcViewDelegate RenderingViewAsync { get; set; }
    }
}
