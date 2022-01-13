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
        private record struct LayoutKey (string ViewPath, string LayoutPath);

        private class CacheEntry
        {
            public IDisposable Callback;
            public ConcurrentDictionary<string, IFluidTemplate> TemplateCache = new();
        }

        private readonly ConcurrentDictionary<IFileProvider, CacheEntry> _cache = new();
        private readonly ConcurrentDictionary<LayoutKey, string> _layoutsCache = new();

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

            if (_fluidViewEngineOptions.RenderingViewAsync != null)
            {
                await _fluidViewEngineOptions.RenderingViewAsync.Invoke(relativePath, context);
            }

            // The body is rendered and buffered before the Layout since it can contain fragments 
            // that need to be rendered as part of the Layout.
            // Also the body or its _ViewStarts might contain a Layout tag.

            var body = await template.RenderAsync(context, _fluidViewEngineOptions.TextEncoder);

            // If a layout is specified while rendering a view, execute it
            if (context.AmbientValues.TryGetValue(Constants.LayoutIndex, out var layoutPath) && layoutPath is string layoutPathString && !String.IsNullOrEmpty(layoutPathString))
            {
                layoutPathString = ResolveLayoutPath(relativePath, layoutPathString, _fluidViewEngineOptions.ViewsFileProvider);

                context.AmbientValues[Constants.ViewPathIndex] = layoutPathString;
                context.AmbientValues[Constants.BodyIndex] = body;

                // Parse the Layout file but ignore viewstarts
                var layoutTemplate = await GetFluidTemplateAsync(layoutPathString, _fluidViewEngineOptions.ViewsFileProvider, includeViewStarts: false);

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
            context.AmbientValues[Constants.ViewPathIndex] = relativePath;

            if (_fluidViewEngineOptions.RenderingViewAsync != null)
            {
                await _fluidViewEngineOptions.RenderingViewAsync.Invoke(relativePath, context);
            }

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

            viewStarts.Reverse();

            return viewStarts;
        }

        protected virtual string ResolveLayoutPath(string viewPath, string layoutPath, IFileProvider fileProvider)
        {
            // When a partial view is referenced by name without a file extension, the following locations are searched in the stated order:
            // Currently executing view's folder
            // Directory graph above the view's folder
            // options.LayoutsLocationFormats

            if (layoutPath.EndsWith(Constants.ViewExtension))
            {
                return Path.Combine(Path.GetDirectoryName(viewPath), layoutPath);
            }

            var key = new LayoutKey(viewPath, layoutPath);

            return _layoutsCache.GetOrAdd(key, k =>
            {
                var layoutPath = k.LayoutPath;
                var viewPath = k.ViewPath;

                int index = viewPath.Length - 1;

                while (!String.IsNullOrEmpty(viewPath))
                {
                    if (index == -1)
                    {
                        return layoutPath;
                    }

                    index = viewPath.LastIndexOf('/', index);

                    viewPath = viewPath.Substring(0, index + 1);

                    var layoutPathPath = Path.Combine(viewPath, layoutPath) + Constants.ViewExtension;

                    var layoutPathInfo = fileProvider.GetFileInfo(layoutPathPath);

                    if (layoutPathInfo.Exists)
                    {
                        return layoutPathPath;
                    }

                    index = index - 1;
                }

                // Not found in hierarchy, fall-back to LayoutsLocationFormats

                foreach (var location in _fluidViewEngineOptions.LayoutsLocationFormats)
                {
                    var layoutPathPath = String.Format(location, Path.GetFileName(layoutPath));

                    var layoutPathInfo = fileProvider.GetFileInfo(layoutPathPath);

                    if (layoutPathInfo.Exists)
                    {
                        return layoutPathPath;
                    }
                }

                return layoutPath;
            });
        }

        protected virtual async ValueTask<IFluidTemplate> GetFluidTemplateAsync(string path, IFileProvider fileProvider, bool includeViewStarts)
        {
            var cache = _cache.GetOrAdd(fileProvider, f =>
            {
                var cacheEntry = new CacheEntry();

                if (_fluidViewEngineOptions.TrackFileChanges)
                {
                    Action<object> callback = null;

                    callback = c =>
                    {
                        // The order here is important. We need to take the token and then apply our changes BEFORE
                        // registering. This prevents us from possible having two change updates to process concurrently.
                        //
                        // If the file changes after we take the token, then we'll process the update immediately upon
                        // registering the callback.

                        var entry = (CacheEntry)c;
                        var previousCallBack = entry.Callback;
                        previousCallBack?.Dispose();
                        var token = fileProvider.Watch("**/*" + Constants.ViewExtension);
                        entry.TemplateCache.Clear();
                        entry.Callback = token.RegisterChangeCallback(callback, c);
                    };

                    cacheEntry.Callback = fileProvider.Watch("**/*" + Constants.ViewExtension).RegisterChangeCallback(callback, cacheEntry);
                }
                return cacheEntry;
            });

            if (cache.TemplateCache.TryGetValue(path, out var template))
            {
                return template;
            }

            template = await ParseLiquidFileAsync(path, fileProvider, includeViewStarts);

            cache.TemplateCache[path] = template;

            return template;
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
