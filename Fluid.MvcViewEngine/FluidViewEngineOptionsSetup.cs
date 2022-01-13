using Fluid.ViewEngine;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;

namespace Fluid.MvcViewEngine
{
    /// <summary>
    /// Defines the default configuration of <see cref="FluidMvcViewOptions"/>.
    /// </summary>
    internal class FluidViewEngineOptionsSetup : ConfigureOptions<FluidMvcViewOptions>
    {
        public FluidViewEngineOptionsSetup(IWebHostEnvironment webHostEnvironment)
            : base(options =>
            {
                options.PartialsFileProvider = new FileProviderMapper(webHostEnvironment.ContentRootFileProvider, "Views");
                options.ViewsFileProvider = new FileProviderMapper(webHostEnvironment.ContentRootFileProvider, "Views");

                options.ViewsLocationFormats.Clear();
                options.ViewsLocationFormats.Add("/{1}/{0}" + Constants.ViewExtension);
                options.ViewsLocationFormats.Add("/Shared/{0}" + Constants.ViewExtension);

                options.PartialsLocationFormats.Clear();
                options.PartialsLocationFormats.Add("{0}" + Constants.ViewExtension);
                options.PartialsLocationFormats.Add("/Partials/{0}" + Constants.ViewExtension);
                options.PartialsLocationFormats.Add("/Partials/{1}/{0}" + Constants.ViewExtension);
                options.PartialsLocationFormats.Add("/Shared/Partials/{0}" + Constants.ViewExtension);

                options.LayoutsLocationFormats.Clear();
                options.LayoutsLocationFormats.Add("/Shared/{0}" + Constants.ViewExtension);
            })
        {
        }
    }    
}
