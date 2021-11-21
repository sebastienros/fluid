using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Fluid.ViewEngine;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.Extensions.Options;

namespace Fluid.MvcViewEngine
{
    public class FluidViewEngine : IFluidViewEngine
    {
        private FluidRendering _fluidRendering;
        private readonly IWebHostEnvironment _hostingEnvironment;
        private const string ControllerKey = "controller";
        private const string AreaKey = "area";
        private FluidMvcViewOptions _options;
        private ConcurrentDictionary<LocationCacheKey, FluidView> _locationCache = new();

        public FluidViewEngine(FluidRendering fluidRendering,
            IOptions<FluidMvcViewOptions> optionsAccessor,
            IWebHostEnvironment hostingEnvironment)
        {
            _options = optionsAccessor.Value;
            _fluidRendering = fluidRendering;
            _hostingEnvironment = hostingEnvironment;
        }

        public ViewEngineResult FindView(ActionContext context, string viewName, bool isMainPage)
        {
            return LocatePageFromViewLocations(context, viewName);
        }

        private ViewEngineResult LocatePageFromViewLocations(ActionContext actionContext, string viewName)
        {
            var controllerName = GetNormalizedRouteValue(actionContext, ControllerKey);
            var areaName = GetNormalizedRouteValue(actionContext, AreaKey);

            var key = new LocationCacheKey(controllerName, areaName, viewName);

            if (_locationCache.TryGetValue(key, out var fluidView))
            {
                return ViewEngineResult.Found(viewName, fluidView);
            }

            var fileProvider = _options.ViewsFileProvider ?? _hostingEnvironment.ContentRootFileProvider;

            List<string> checkedLocations = null;

            foreach (var location in _options.ViewsLocationFormats)
            {
                var view = String.Format(location, viewName, controllerName, areaName);

                if (fileProvider.GetFileInfo(view).Exists)
                {
                    _locationCache[key] = fluidView = new FluidView(view, _fluidRendering);
                    return ViewEngineResult.Found(viewName, fluidView);
                }

                checkedLocations ??= new();
                checkedLocations.Add(view);
            }

            return ViewEngineResult.NotFound(viewName, checkedLocations);
        }

        public ViewEngineResult GetView(string executingFilePath, string viewPath, bool isMainPage)
        {
            var applicationRelativePath = GetAbsolutePath(executingFilePath, viewPath);

            if (!(IsApplicationRelativePath(viewPath) || IsRelativePath(viewPath)))
            {
                // Not a path this method can handle.
                return ViewEngineResult.NotFound(applicationRelativePath, Enumerable.Empty<string>());
            }

            return ViewEngineResult.Found("Default", new FluidView(applicationRelativePath, _fluidRendering));
        }

        public string GetAbsolutePath(string executingFilePath, string pagePath)
        {
            if (string.IsNullOrEmpty(pagePath))
            {
                // Path is not valid; no change required.
                return pagePath;
            }

            if (IsApplicationRelativePath(pagePath))
            {
                // An absolute path already; no change required.
                return pagePath.Replace("~/", "");
            }

            if (!IsRelativePath(pagePath))
            {
                // A page name; no change required.
                return pagePath;
            }

            // Given a relative path i.e. not yet application-relative (starting with "~/" or "/"), interpret
            // path relative to currently-executing view, if any.
            if (string.IsNullOrEmpty(executingFilePath))
            {
                // Not yet executing a view. Start in app root.
                return "/" + pagePath;
            }

            // Get directory name (including final slash) but do not use Path.GetDirectoryName() to preserve path
            // normalization.
            var index = executingFilePath.LastIndexOf('/');
            Debug.Assert(index >= 0);
            return executingFilePath.Substring(0, index + 1) + pagePath;
        }


        private static bool IsApplicationRelativePath(string name)
        {
            Debug.Assert(!string.IsNullOrEmpty(name));
            return name[0] == '~' || name[0] == '/';
        }

        private static bool IsRelativePath(string name)
        {
            Debug.Assert(!string.IsNullOrEmpty(name));

            // Though ./ViewName looks like a relative path, framework searches for that view using view locations.
            return name.EndsWith(Constants.ViewExtension, StringComparison.OrdinalIgnoreCase);
        }

        public static string GetNormalizedRouteValue(ActionContext context, string key)
        {
            if (context == null)
            {
                ExceptionHelper.ThrowArgumentNullException(nameof(context));
            }

            if (key == null)
            {
                ExceptionHelper.ThrowArgumentNullException(nameof(key));
            }

            if (!context.RouteData.Values.TryGetValue(key, out object routeValue))
            {
                return null;
            }

            var actionDescriptor = context.ActionDescriptor;
            string normalizedValue = null;

            if (actionDescriptor.RouteValues.TryGetValue(key, out string value) && !string.IsNullOrEmpty(value))
            {
                normalizedValue = value;
            }

            var stringRouteValue = routeValue?.ToString();
            if (string.Equals(normalizedValue, stringRouteValue, StringComparison.OrdinalIgnoreCase))
            {
                return normalizedValue;
            }

            return stringRouteValue;
        }

        public readonly record struct LocationCacheKey(string ControllerName, string AreaName, string ViewName);
    }
}
