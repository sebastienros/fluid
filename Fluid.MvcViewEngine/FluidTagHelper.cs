using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace FluidMvcViewEngine
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

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            var result = await _fluidRendering.Render(View, Model, null, null);
            output.TagName = null;
            output.Content.AppendHtml(result);
        }

    }
}
