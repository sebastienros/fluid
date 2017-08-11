using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Fluid.Ast;
using Fluid.Tags;
using FluidMvcViewEngine;

namespace Fluid.MvcViewEngine.Tags
{
    public class LayoutTag : ExpressionTag
    {
        public override async Task<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context, Expression path)
        {
            var relativeLayoutPath = (await path.EvaluateAsync(context)).ToStringValue();
            if (!relativeLayoutPath.EndsWith(FluidViewEngine.ViewExtension))
            {
                relativeLayoutPath += FluidViewEngine.ViewExtension;
            }

            var currentViewPath = context.AmbientValues[FluidRendering.ViewPath] as string;
            var currentDirectory = Path.GetDirectoryName(currentViewPath);
            var layoutPath = Path.Combine(currentDirectory, relativeLayoutPath);

            context.AmbientValues["Layout"] = layoutPath;

            return Completion.Normal;
        }
    }
}
