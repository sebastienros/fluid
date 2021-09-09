using Fluid.ViewEngine;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;

namespace Fluid.MvcViewEngine
{
    public class FluidViewEngineOptionsSetup : ConfigureOptions<FluidViewEngineOptions>
    {
        public FluidViewEngineOptionsSetup(IWebHostEnvironment webHostEnvironment)
            : base(options =>
            {
                options.IncludesFileProvider = (options.IncludesFileProvider ?? new FileProviderMapper(webHostEnvironment.ContentRootFileProvider, "Views/Partials"));
                options.ViewsFileProvider = webHostEnvironment.ContentRootFileProvider;
                options.ViewLocationFormats.Add("Views/{1}/{0}" + FluidViewEngine.ViewExtension);
                options.ViewLocationFormats.Add("Views/Shared/{0}" + FluidViewEngine.ViewExtension);
            })
        {
        }
    }    
}
