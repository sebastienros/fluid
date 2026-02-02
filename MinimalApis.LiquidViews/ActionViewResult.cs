using Fluid;
using Fluid.ViewEngine;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using Fluid.Utils;

namespace MinimalApis.LiquidViews
{
    public class ActionViewResult : IResult
    {
        private readonly string _viewName;
        private readonly object _model;

        private readonly static ConcurrentDictionary<string, string> _viewLocationsCache = new();

        public ActionViewResult(string viewName)
        {
            _viewName = viewName;
            _model = new object();
        }

        public ActionViewResult(string viewName, object model)
        {
            _viewName = viewName;
            _model = model;
        }

        public string ContentType { get; set; } = "text/html";

        public async Task ExecuteAsync(HttpContext httpContext)
        {
            var fluidViewRenderer = httpContext.RequestServices.GetService<IFluidViewRenderer>();
            var options = httpContext.RequestServices.GetService<IOptions<FluidViewEngineOptions>>().Value;

            var viewPath = LocatePageFromViewLocations(_viewName, options);

            if (viewPath == null)
            {
                httpContext.Response.StatusCode = 404;
                return;
            }

            httpContext.Response.StatusCode = 200;
            httpContext.Response.ContentType = ContentType;

            var context = new TemplateContext(_model, options.TemplateOptions);
            context.Options.FileProvider = options.PartialsFileProvider;

            await using var sw = new StreamWriter(httpContext.Response.Body);
            var bufferSize = context.Options.OutputBufferSize;
            if (bufferSize <= 0)
            {
                bufferSize = 16 * 1024;
            }

            await using var output = new TextWriterFluidOutput(sw, bufferSize, leaveOpen: true);
            await fluidViewRenderer.RenderViewAsync(output, viewPath, context);
            await output.FlushAsync();
        }

        private static string LocatePageFromViewLocations(string viewName, FluidViewEngineOptions options)
        {
            if (_viewLocationsCache.TryGetValue(viewName, out var cachedLocation) && cachedLocation != null)
            {
                return cachedLocation;
            }

            var fileProvider = options.ViewsFileProvider;

            foreach (var location in options.ViewsLocationFormats)
            {
                var viewFilename = Path.Combine(String.Format(location, viewName));

                var fileInfo = fileProvider.GetFileInfo(viewFilename);

                if (fileInfo.Exists)
                {
                    _viewLocationsCache[viewName] = viewFilename;
                    return viewFilename;
                }
            }

            return null;
        }
    }
}
