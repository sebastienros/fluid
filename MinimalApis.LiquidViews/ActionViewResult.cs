using Fluid;
using Fluid.ViewEngine;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MinimalApis.LiquidViews
{
    public class ActionViewResult : IResult
    {
        private readonly string _viewName;
        private readonly object _model;

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
            var context = new TemplateContext(_model, options.TemplateOptions)
            {
                CancellationToken = httpContext.RequestAborted
            };
            context.Options.FileProvider =
                options.PartialsFileProvider ??
                options.ViewsFileProvider ??
                options.TemplateOptions.FileProvider;

            var viewPath = await LocatePageFromViewLocationsAsync(
                _viewName,
                options,
                context,
                httpContext.RequestAborted);

            if (viewPath == null)
            {
                httpContext.Response.StatusCode = 404;
                return;
            }

            httpContext.Response.StatusCode = 200;
            httpContext.Response.ContentType = ContentType;

            var bufferSize = context.Options.OutputBufferSize;
            if (bufferSize <= 0)
            {
                bufferSize = 16 * 1024;
            }

            await using var output = new PipeWriterFluidOutput(
                httpContext.Response.BodyWriter,
                bufferSize,
                httpContext.RequestAborted);
            await fluidViewRenderer.RenderViewAsync(output, viewPath, context);
            await output.FlushAsync();
        }

        private static async ValueTask<string> LocatePageFromViewLocationsAsync(
            string viewName,
            FluidViewEngineOptions options,
            TemplateContext context,
            CancellationToken cancellationToken)
        {
            var fileProvider = options.ViewsFileProvider ?? options.TemplateOptions.FileProvider;

            foreach (var location in options.ViewsLocationFormats)
            {
                var viewFilename = Path.Combine(String.Format(location, viewName));

                var fileInfo = await fileProvider.GetFileInfoAsync(viewFilename, context, cancellationToken);

                if (fileInfo != null)
                {
                    return viewFilename;
                }
            }

            return null;
        }
    }
}
