using Fluid.Ast;
using Fluid.MvcViewEngine.Internal;
using Fluid.Parser;
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
        private const string ViewStartFilename = "_ViewStart.liquid";
        public const string ViewPath = "ViewPath";

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
            var context = new TemplateContext(model, _options.TemplateOptions);
            context.SetValue("ViewData", viewData);
            context.SetValue("ModelState", modelState);

            // Provide some services to all statements
            context.AmbientValues[ViewPath] = path;
            context.AmbientValues["Sections"] = new Dictionary<string, IReadOnlyList<Statement>>();

            var template = ParseLiquidFile(path, _options.ViewsFileProvider ?? _hostingEnvironment.ContentRootFileProvider, true);

            var body = await template.RenderAsync(context, _options.TextEncoder);

            // If a layout is specified while rendering a view, execute it
            if (context.AmbientValues.TryGetValue("Layout", out var layoutPath))
            {
                context.AmbientValues[ViewPath] = layoutPath;
                context.AmbientValues["Body"] = body;
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

        public IFluidTemplate ParseLiquidFile(string path, IFileProvider fileProvider, bool includeViewStarts)
        {
            return _memoryCache.GetOrCreate(path, viewEntry =>
            {
                var statements = new List<Statement>();

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
                        statements.Add(new CallbackStatement((writer, encoder, context) =>
                        {
                            context.AmbientValues[ViewPath] = viewStartPath;
                            return new ValueTask<Completion>(Completion.Normal);
                        }));

                        var viewStartTemplate = ParseLiquidFile(viewStartPath, fileProvider, false);

                        statements.AddRange(viewStartTemplate.Statements);
                    }
                }

                using (var stream = fileInfo.CreateReadStream())
                {
                    using (var sr = new StreamReader(stream))
                    {
                        var fileContent = sr.ReadToEnd();
                        if (_options.Parser.TryParse(fileContent, out var template, out var errors))
                        {
                            statements.AddRange(template.Statements);

                            return new FluidTemplate(statements);
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
