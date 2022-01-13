using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Fluid.MvcViewEngine
{
    [HtmlTargetElement("fluid")]
    public class FluidTagHelper : TagHelper
    {
        private FluidRendering _fluidRendering { get; set; }

        public FluidTagHelper(FluidRendering fluidRendering)
        {
            _fluidRendering = fluidRendering;
        }

        [HtmlAttributeNotBound]
        [ViewContext]
        public ViewContext ViewContext { get; set; }

        [HtmlAttributeName("model")]
        public object Model { get; set; }

        [HtmlAttributeName("view")]
        public string View { get; set; }

        public override Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            static async Task Awaited(TagHelperOutput o, StringWriter sw, Task t)
            {
                await t;
                o.TagName = null;
                o.Content.AppendHtml(sw.ToString());
            }

            using (var sw = new StringWriter())
            {
                var task = _fluidRendering.RenderAsync(sw, View, ViewContext);

                if (task.IsCompletedSuccessfully)
                {
                    output.TagName = null;
                    output.Content.AppendHtml(sw.ToString());
                    return Task.FromResult(output);
                }

                return Awaited(output, sw, task);
            }
        }
    }
}
