using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Fluid.MvcViewEngine
{
    [HtmlTargetElement("fluid")]
    public class FluidTagHelper : TagHelper
    {
        public IFluidRendering _fluidRendering { get; set; }

        public FluidTagHelper(IFluidRendering fluidRendering)
        {
            _fluidRendering = fluidRendering;
        }

        [HtmlAttributeName("model")]
        public object Model { get; set; }

        [HtmlAttributeName("view")]
        public string View { get; set; }

        public override Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            static async Task Awaited(TagHelperOutput o, ValueTask<string> t)
            {
                o.TagName = null;
                o.Content.AppendHtml(await t);
            }

            var task = _fluidRendering.RenderAsync(View, Model, null, null);
            if (task.IsCompletedSuccessfully)
            {
                output.TagName = null;
                output.Content.AppendHtml(task.Result);
                return Task.FromResult(output);
            }

            return Awaited(output, task);
        }
    }
}
