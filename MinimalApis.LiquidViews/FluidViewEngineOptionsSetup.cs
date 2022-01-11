using Fluid;
using Fluid.ViewEngine;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;

namespace MinimalApis.LiquidViews
{
    public class FluidViewEngineOptionsSetup : ConfigureOptions<FluidViewEngineOptions>
    {
        public FluidViewEngineOptionsSetup(IWebHostEnvironment webHostEnvironment)
            : base(options =>
            {
                options.PartialsFileProvider = new FileProviderMapper(webHostEnvironment.ContentRootFileProvider, "Views");
                options.ViewsFileProvider = new FileProviderMapper(webHostEnvironment.ContentRootFileProvider, "Views");

                options.TemplateOptions.MemberAccessStrategy = UnsafeMemberAccessStrategy.Instance;

                options.ViewsLocationFormats.Clear();
                options.ViewsLocationFormats.Add("/{0}" + Constants.ViewExtension);

                options.PartialsLocationFormats.Clear();
                options.PartialsLocationFormats.Add("{0}" + Constants.ViewExtension);
                options.PartialsLocationFormats.Add("/Partials/{0}" + Constants.ViewExtension);

                options.LayoutsLocationFormats.Clear();
                options.LayoutsLocationFormats.Add("/Shared/{0}" + Constants.ViewExtension);

            })
        {
        }
    }    
}
