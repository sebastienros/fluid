using Fluid.Ast;
using Fluid.Parser;
using Microsoft.Extensions.FileProviders;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Fluid.ViewEngine
{
    /// <summary>
    /// This class is registered as a singleton.
    /// </summary>
    public class FluidViewRenderer : IFluidViewRenderer
    {
        private readonly ConcurrentDictionary<string, IFluidTemplate> _cache = new ConcurrentDictionary<string, IFluidTemplate>();

        public FluidViewRenderer(FluidViewEngineOptions fluidViewEngineOptions)
        {
            _fluidViewEngineOptions = fluidViewEngineOptions;

            _fluidViewEngineOptions.TemplateOptions.FileProvider = _fluidViewEngineOptions.PartialsFileProvider ?? _fluidViewEngineOptions.ViewsFileProvider ?? new NullFileProvider();
        }

        private readonly FluidViewEngineOptions _fluidViewEngineOptions;

        public virtual async Task RenderViewAsync(TextWriter writer, string relativePath, TemplateContext context)
        {
            // Provide some services to all statements
            context.AmbientValues[Constants.ViewPathIndex] = relativePath;
            context.AmbientValues[Constants.SectionsIndex] = null; // it is lazily initialized when first used
            context.AmbientValues[Constants.RendererIndex] = this;

            var template = await GetFluidTemplateAsync(relativePath, _fluidViewEngineOptions.ViewsFileProvider, true);

            // The body is rendered and buffer before the Layout since it can contain fragments 
            // that need to be rendered as part of the Layout.
            // Also the body or its _ViewStarts might contain a Layout tag.

            var body = await template.RenderAsync(context, _fluidViewEngineOptions.TextEncoder);

            // If a layout is specified while rendering a view, execute it
            if (context.AmbientValues.TryGetValue(Constants.LayoutIndex, out var layoutPath) && !String.IsNullOrEmpty(Convert.ToString(layoutPath)))
            {
                context.AmbientValues[Constants.ViewPathIndex] = layoutPath;
                context.AmbientValues[Constants.BodyIndex] = body;
                var layoutTemplate = await GetFluidTemplateAsync((string)layoutPath, _fluidViewEngineOptions.ViewsFileProvider, false);

                await layoutTemplate.RenderAsync(writer, _fluidViewEngineOptions.TextEncoder, context);
            }
            else
            {
                writer.Write(body);
            }
        }

        public virtual async Task RenderPartialAsync(TextWriter writer, string relativePath, TemplateContext context)
        {
            // Substitute View Path
            context.AmbientValues.TryGetValue(Constants.ViewPathIndex, out var viewPath);
            context.AmbientValues[Constants.ViewPathIndex] = relativePath;

            var template = await GetFluidTemplateAsync(relativePath, _fluidViewEngineOptions.PartialsFileProvider, false);

            await template.RenderAsync(writer, _fluidViewEngineOptions.TextEncoder, context);
        }

        protected virtual List<string> FindViewStarts(string viewPath, IFileProvider fileProvider)
        {
            var viewStarts = new List<string>();
            int index = viewPath.Length - 1;
            
            while (!String.IsNullOrEmpty(viewPath))
            {
                if (index == -1)
                {
                    return viewStarts;
                }

                index = viewPath.LastIndexOf('/', index);

                viewPath = viewPath.Substring(0, index + 1);

                var viewStartPath = viewPath + Constants.ViewStartFilename;

                var viewStartInfo = fileProvider.GetFileInfo(viewStartPath);

                if (viewStartInfo.Exists)
                {
                    viewStarts.Add(viewStartPath);
                }

                index = index - 1;
            }

            return viewStarts;
        }

        protected async ValueTask<IFluidTemplate> GetFluidTemplateAsync(string path, IFileProvider fileProvider, bool includeViewStarts)
        {
            if (TryGetCachedTemplate(path, out var template))
            {
                return template;
            }

            template = await ParseLiquidFileAsync(path, fileProvider, includeViewStarts);

            SetCachedTemplate(path, template);

            return template;
        }

        protected virtual bool TryGetCachedTemplate(string path, out IFluidTemplate template)
        {
            return _cache.TryGetValue(path, out template);
        }

        protected virtual void SetCachedTemplate(string path, IFluidTemplate template)
        {
            _cache[path] = template;
        }

        protected virtual async ValueTask<IFluidTemplate> ParseLiquidFileAsync(string path, IFileProvider fileProvider, bool includeViewStarts)
        {
            var fileInfo = fileProvider.GetFileInfo(path);

            if (!fileInfo.Exists)
            {
                return new FluidTemplate();
            }

            var subTemplates = new List<IFluidTemplate>();
                
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

                    var viewStartTemplate = await GetFluidTemplateAsync(viewStartPath, fileProvider, false);

                    subTemplates.Add(callbackTemplate);
                    subTemplates.Add(viewStartTemplate);
                }
            }

            using (var stream = fileInfo.CreateReadStream())
            {
                using (var sr = new StreamReader(stream))
                {
                    var fileContent = sr.ReadToEnd();
                    if (_fluidViewEngineOptions.Parser.TryParse(fileContent, out var template, out var errors))
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
        }
    }
}
