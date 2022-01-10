using MinimalApis.LiquidViews;

namespace Microsoft.AspNetCore.Http
{
    public static class ResultExtensions
    {
        public static IResult View(this IResultExtensions result, string viewName)
        {
            return new ActionViewResult(viewName);
        }

        public static IResult View(this IResultExtensions result, string viewName, object model)
        {
            return new ActionViewResult(viewName, model);
        }
    }
}
