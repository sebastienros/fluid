using Fluid.Ast;
using Fluid.Parser;
using Fluid.ViewEngine;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Fluid.MvcViewEngine
{
    /// <summary>
    /// This class is registered as a singleton. As such it can store application wide 
    /// state.
    /// </summary>
    public class FluidRendering : IFluidRendering
    {
        static FluidRendering()
        {
        }

        public FluidRendering(
            IMemoryCache memoryCache,
            IOptions<FluidViewEngineOptions> optionsAccessor,
            IWebHostEnvironment hostingEnvironment)
        {
            _memoryCache = memoryCache;
            _hostingEnvironment = hostingEnvironment;
            _options = optionsAccessor.Value;

            _options.TemplateOptions.MemberAccessStrategy.Register<ViewDataDictionary>();
            _options.TemplateOptions.MemberAccessStrategy.Register<ModelStateDictionary>();
            _options.TemplateOptions.FileProvider = new FileProviderMapper(_options.IncludesFileProvider ?? _hostingEnvironment.ContentRootFileProvider, "Views");
        }

        private readonly IMemoryCache _memoryCache;
        private readonly IWebHostEnvironment _hostingEnvironment;
        private readonly FluidViewEngineOptions _options;

        public async ValueTask<string> RenderAsync(string path, object model, ViewDataDictionary viewData, ModelStateDictionary modelState)
        {
            var context = new TemplateContext(_options.TemplateOptions);
            context.SetValue("ViewData", viewData);
            context.SetValue("ModelState", modelState);
            context.SetValue("Model", model);

            // Provide some services to all statements
            context.AmbientValues[Constants.ViewPathIndex] = path;
            context.AmbientValues[Constants.SectionsIndex] = new Dictionary<string, IReadOnlyList<Statement>>();

            var template = ParseLiquidFile(path, _options.ViewsFileProvider ?? _hostingEnvironment.ContentRootFileProvider, true);

            var body = await template.RenderAsync(context, _options.TextEncoder);

            // If a layout is specified while rendering a view, execute it
            if (context.AmbientValues.TryGetValue(Constants.LayoutIndex, out var layoutPath))
            {
                context.AmbientValues[Constants.ViewPathIndex] = layoutPath;
                context.AmbientValues[Constants.BodyIndex] = body;
                var layoutTemplate = ParseLiquidFile((string)layoutPath, _options.ViewsFileProvider ?? _hostingEnvironment.ContentRootFileProvider, false);

                return await layoutTemplate.RenderAsync(context, _options.TextEncoder);
            }

            return body;
        }

        public List<string> FindViewStarts(string viewPath, IFileProvider fileProvider)
        {
            var viewStarts = new List<string>();
            int index = viewPath.Length - 1;
            while (!String.IsNullOrEmpty(viewPath) &&
                !(String.Equals(viewPath, "Views", StringComparison.OrdinalIgnoreCase)))
            {
                index = viewPath.LastIndexOf('/', index);

                if (index == -1)
                {
                    return viewStarts;
                }

                viewPath = viewPath.Substring(0, index + 1);

                var viewStartPath = viewPath + Constants.ViewStartFilename;

                var viewStartInfo = fileProvider.GetFileInfo(viewStartPath);

                if (viewStartInfo.Exists)
                {
                    viewStarts.Add(viewStartPath);
                }
                else
                {
                    // Try with the lower cased version for backward compatibility, c.f. https://github.com/sebastienros/fluid/issues/361

                    viewStartPath = viewPath + Constants.ViewStartFilename.ToLowerInvariant();

                    viewStartInfo = fileProvider.GetFileInfo(viewStartPath);

                    if (viewStartInfo.Exists)
                    {
                        viewStarts.Add(viewStartPath);
                    }
                }

                index = index - 1;
            }

            return viewStarts;
        }

        public IFluidTemplate ParseLiquidFile(string path, IFileProvider fileProvider, bool includeViewStarts)
        {
            return _memoryCache.GetOrCreate(path, viewEntry =>
            {
                var subTemplates = new List<IFluidTemplate>();

                // Default sliding expiration to prevent the entries for being kept indefinitely
                viewEntry.SlidingExpiration = TimeSpan.FromHours(1);

                var fileInfo = fileProvider.GetFileInfo(path);
                viewEntry.ExpirationTokens.Add(fileProvider.Watch(path));

                if (includeViewStarts)
                {
                    // Add ViewStart files
                    foreach (var viewStartPath in FindViewStarts(path, fileProvider))
                    {
                        // Redefine the current view path while processing ViewStart files
                        var callbackTemplate = new FluidTemplate(new CallbackStatement((writer, encoder, context) =>
                        {
                            context.AmbientValues[Constants.ViewPathIndex] = viewStartPath;
                            return new ValueTask<Completion>(Completion.Normal);
                        }));

                        var viewStartTemplate = ParseLiquidFile(viewStartPath, fileProvider, false);

                        subTemplates.Add(callbackTemplate);
                        subTemplates.Add(viewStartTemplate);
                    }
                }

                using (var stream = fileInfo.CreateReadStream())
                {
                    using (var sr = new StreamReader(stream))
                    {
                        var fileContent = sr.ReadToEnd();
                        if (_options.Parser.TryParse(fileContent, out var template, out var errors))
                        {
                            subTemplates.Add(template);

                            return new CompositeFluidTemplate(subTemplates);
                        }
                        else
                        {
                            throw new ParseException(errors);
                        }
                    }
                }
            });
        }
    }
}
