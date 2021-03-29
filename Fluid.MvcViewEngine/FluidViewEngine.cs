using System;
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
        private IFluidRendering _fluidRendering;
        private readonly IWebHostEnvironment _hostingEnvironment;
        public static readonly string ViewExtension = ".liquid";
        private const string ControllerKey = "controller";
        private const string AreaKey = "area";
        private FluidViewEngineOptions _options;

        public FluidViewEngine(IFluidRendering fluidRendering,
            IOptions<FluidViewEngineOptions> optionsAccessor,
            IWebHostEnvironment hostingEnvironment)
        {
            _options = optionsAccessor.Value;
            _fluidRendering = fluidRendering;
            _hostingEnvironment = hostingEnvironment;
        }

        public ViewEngineResult FindView(ActionContext context, string viewName, bool isMainPage)
        {
            return LocatePageFromViewLocations(context, viewName, isMainPage);
        }

        private ViewEngineResult LocatePageFromViewLocations(
            ActionContext actionContext,
            string viewName,
            bool isMainPage)
        {
            var controllerName = GetNormalizedRouteValue(actionContext, ControllerKey);
            var areaName = GetNormalizedRouteValue(actionContext, AreaKey);

            var fileProvider = _options.ViewsFileProvider ?? _hostingEnvironment.ContentRootFileProvider;

            var checkedLocations = new List<string>();
            foreach (var location in _options.ViewLocationFormats)
            {
                var view = string.Format(location, viewName, controllerName);
                if(fileProvider.GetFileInfo(view).Exists)
                    return ViewEngineResult.Found("Default", new FluidView(view, _fluidRendering));
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
            return name.EndsWith(ViewExtension, StringComparison.OrdinalIgnoreCase);
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

            if (actionDescriptor.RouteValues.TryGetValue(key, out string value) &&
                !string.IsNullOrEmpty(value))
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
    }
}
