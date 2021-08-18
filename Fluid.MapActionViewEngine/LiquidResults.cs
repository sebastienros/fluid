using Fluid.MapActionViewEngine;

namespace Microsoft.AspNetCore.Http
{
    public static class LiquidResults
    {
        public static IResult View(string viewName)
        {
            return new ActionViewResult(viewName);
        }

        public static IResult View(string viewName, object model)
        {
            return new ActionViewResult(viewName, model);
        }

        public static IResult View(string viewName, string area, object model)
        {
            return new ActionViewResult(viewName, area, model);
        }
    }
}
