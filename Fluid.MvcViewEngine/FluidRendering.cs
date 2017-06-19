using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Fluid;
using Fluid.Ast;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft​.Extensions​.Caching​.Memory;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

namespace FluidMvcViewEngine
{
    public class FluidRendering : IFluidRendering
    {
        private const string ViewStartFilename = "_ViewStart.liquid";

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
                var result = new List<Statement>();

                // Default sliding expiration to prevent the entries for being kept indefinitely
                entry.SlidingExpiration = TimeSpan.FromHours(1);

                // Check for a custom file provider
                var fileProvider = _options.FileProvider ?? _hostingEnvironment.ContentRootFileProvider;
                
                var fileInfo = fileProvider.GetFileInfo(path);
                entry.ExpirationTokens.Add(fileProvider.Watch(path));

                var source = File.ReadAllText(path);

                if (FluidTemplate.TryParse(source, out var temp, out var errors))
                {
                    // Add ViewStart files

                    foreach (var viewStartPath in FindViewStarts(path, fileProvider))
                    {
                        using (var stream = fileProvider.GetFileInfo(viewStartPath).CreateReadStream())
                        {
                            using (var sr = new StreamReader(stream))
                            {
                                if (FluidTemplate.TryParse(sr.ReadToEnd(), out var viewStartTemplate, out errors))
                                {
                                    result.AddRange(viewStartTemplate.Statements);
                                    entry.ExpirationTokens.Add(fileProvider.Watch(viewStartPath));
                                }
                                else
                                {
                                    throw new Exception(String.Join(Environment.NewLine, errors));
                                }
                            }
                        }
                    }

                    result.AddRange(temp.Statements);

                    return result;
                }

                throw new Exception(String.Join(Environment.NewLine, errors));
            });

            var template = new FluidTemplate(statements);

            var context = new TemplateContext();
            context.LocalScope.SetValue("Model", model);
            context.LocalScope.SetValue("ViewData", viewData);
            context.LocalScope.SetValue("ModelState", modelState);
                        
            return template.RenderAsync(context);
        }

        public IEnumerable<string> FindViewStarts(string viewPath, IFileProvider fileProvider)
        {
            var viewStarts = new List<string>();
            int index = viewPath.Length - 1;
            while(! String.IsNullOrEmpty(viewPath) &&
                !(String.Equals(viewPath, "Views", StringComparison.OrdinalIgnoreCase)))
            {
                index = viewPath.LastIndexOf('/', index);

                if (index == -1)
                {
                    return viewStarts;
                }

                viewPath = viewPath.Substring(0, index + 1) + ViewStartFilename;

                var viewStartInfo = fileProvider.GetFileInfo(viewPath);
                if (viewStartInfo.Exists)
                {
                    viewStarts.Add(viewPath);
                }

                index = index - 1;
            }

            return viewStarts;
        }
    }
}
