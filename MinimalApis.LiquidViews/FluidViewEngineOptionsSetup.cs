using System;
using System.Text.Encodings.Web;
using Fluid.ViewEngine;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MinimalApis.LiquidViews;

public class FluidViewEngineOptionsSetup : ConfigureOptions<FluidViewEngineOptions>
{
    public FluidViewEngineOptionsSetup(IWebHostEnvironment webHostEnvironment, IServiceProvider serviceProvider)
        : base(options =>
        {
            // Use the app's HtmlEncoder service when there is one, so views encode like the rest of the app.
            options.TextEncoder = serviceProvider.GetService<HtmlEncoder>() ?? FluidViewEngineOptions.DefaultTextEncoder;

            options.PartialsFileProvider = new FileProviderMapper(webHostEnvironment.ContentRootFileProvider, "Views");
            options.ViewsFileProvider = new FileProviderMapper(webHostEnvironment.ContentRootFileProvider, "Views");

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
