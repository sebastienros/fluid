using Fluid.ViewEngine;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
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
        private readonly FluidViewRenderer _fluidViewRenderer;

        public FluidRendering(
            IOptions<FluidMvcViewOptions> optionsAccessor,
            IWebHostEnvironment hostingEnvironment)
        {
            _hostingEnvironment = hostingEnvironment;
            _options = optionsAccessor.Value;

            _options.TemplateOptions.MemberAccessStrategy.Register<ViewDataDictionary>();
            _options.TemplateOptions.MemberAccessStrategy.Register<ModelStateDictionary>();
            _options.TemplateOptions.FileProvider = new FileProviderMapper(_options.IncludesFileProvider ?? _hostingEnvironment.ContentRootFileProvider, _options.ViewsPath);

            _fluidViewRenderer = new FluidViewRenderer(_options);

            _options.ViewsFileProvider ??= _hostingEnvironment.ContentRootFileProvider;
        }

        private readonly IWebHostEnvironment _hostingEnvironment;
        private readonly FluidViewEngineOptions _options;

        public async Task RenderAsync(TextWriter writer, string path, object model, ViewDataDictionary viewData, ModelStateDictionary modelState)
        {
            var context = new TemplateContext(_options.TemplateOptions);
            context.SetValue("ViewData", viewData);
            context.SetValue("ModelState", modelState);
            context.SetValue("Model", model);

            await _fluidViewRenderer.RenderViewAsync(writer, path, context);
        }
    }
}
