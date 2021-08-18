using Fluid.ViewEngine;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Fluid.MapActionViewEngine
{
    public class ActionViewResult : IResult
    {
        private readonly string _viewName;
        private readonly string _area;
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

        public ActionViewResult(string viewName, string area, object model)
        {
            _viewName = viewName;
            _area = area;
            _model = model;
        }

        public string ContentType { get; set; } = "text/html";

        public async Task ExecuteAsync(HttpContext httpContext)
        {
            var fluidViewRenderer = httpContext.RequestServices.GetService<IFluidViewRenderer>();
            var options = httpContext.RequestServices.GetService<IOptions<FluidViewEngineOptions>>().Value;

            var viewPath = LocatePageFromViewLocations(_viewName, _area, options);

            if (viewPath == null)
            {
                httpContext.Response.StatusCode = 404;
                return;
            }

            httpContext.Response.StatusCode = 200;
            httpContext.Response.ContentType = ContentType;

            await using var sw = new StreamWriter(httpContext.Response.Body);
            await fluidViewRenderer.RenderViewAsync(sw, viewPath, _model);
        }

        private static string LocatePageFromViewLocations(string viewName, string area, FluidViewEngineOptions options)
        {
            var fileProvider = options.ViewsFileProvider;

            foreach (var location in options.ViewLocationFormats)
            {
                var viewPath = string.Format(location, viewName, area);
                var fileInfo = fileProvider.GetFileInfo(viewPath);
                
                if (fileInfo.Exists)
                {
                    return viewPath;
                }
            }

            return null;
        }
    }
}
