using System;
using System.IO;
using System.Threading.Tasks;
using Fluid;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft​.Extensions​.Caching​.Memory;
using Microsoft.Extensions.Options;

namespace FluidMvcViewEngine
{
    public class FluidRendering : IFluidRendering
    {
        static FluidRendering()
        {
            // TemplateContext.GlobalMemberAccessStrategy.Register<ViewDataDictionary>();
            TemplateContext.GlobalMemberAccessStrategy.Register<ModelStateDictionary>();
        }

        public FluidRendering(
            IMemoryCache memoryCache,
            IOptions<FluidViewEngineOptions> optionsAccessor,
            IHostingEnvironment hostingEnvironment)
        {
            _memoryCache = memoryCache;
            _hostingEnvironment = hostingEnvironment;
            _options = optionsAccessor.Value;
        }

        private readonly IMemoryCache _memoryCache;
        private readonly IHostingEnvironment _hostingEnvironment;
        private FluidViewEngineOptions _options;

        public Task<string> Render(string path, object model, ViewDataDictionary viewData, ModelStateDictionary modelState)
        {
            var statements = _memoryCache.GetOrCreate(path, entry =>
            {
                entry.SlidingExpiration = TimeSpan.FromHours(1);

                var fileProvider = _options.FileProvider ?? _hostingEnvironment.ContentRootFileProvider;
                entry.ExpirationTokens.Add(fileProvider.Watch(path));
                
                var source = File.ReadAllText(path);

                if (FluidTemplate.TryParse(source, out var temp, out var errors))
                {
                    return temp.Statements;
                }

                throw new Exception(String.Join("\r\n", errors));
            });

            var template = new FluidTemplate(statements);

            var context = new TemplateContext();
            context.LocalScope.SetValue("Model", model);
            context.LocalScope.SetValue("ViewData", viewData);
            context.LocalScope.SetValue("ModelState", modelState);
                        
            return template.RenderAsync(context);
        }
    }
}
