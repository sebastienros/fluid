using Fluid.ViewEngine;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;

namespace Fluid.MapActionViewEngine
{
    public class FluidViewEngineOptionsSetup : ConfigureOptions<FluidViewEngineOptions>
    {
        public FluidViewEngineOptionsSetup(IWebHostEnvironment webHostEnvironment)
            : base(options =>
            {
                options.IncludesFileProvider = webHostEnvironment.ContentRootFileProvider;
                options.ViewsFileProvider = webHostEnvironment.ContentRootFileProvider;
                options.ViewLocationFormats.Add("Views/{0}" + Constants.ViewExtension);
                options.ViewLocationFormats.Add("Views/{1}/{0}" + Constants.ViewExtension);
                options.ViewLocationFormats.Add("Views/Shared/{0}" + Constants.ViewExtension);
                options.TemplateOptions.MemberAccessStrategy = UnsafeMemberAccessStrategy.Instance;
            })
        {
        }
    }    
}
