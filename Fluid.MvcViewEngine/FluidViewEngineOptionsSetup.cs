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
                options.IncludesFileProvider = webHostEnvironment.ContentRootFileProvider;
                options.ViewsFileProvider = webHostEnvironment.ContentRootFileProvider;
                options.ViewLocationFormats.Add("Views/{1}/{0}" + FluidViewEngine.ViewExtension);
                options.ViewLocationFormats.Add("Views/Shared/{0}" + FluidViewEngine.ViewExtension);
            })
        {
        }
    }    
}
