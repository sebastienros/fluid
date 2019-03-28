using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Fluid.Ast;
using Fluid.Tags;

namespace Fluid.MvcViewEngine.Tags
{
    public class LayoutTag : ExpressionTag
    {
        public override async ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context, Expression expression)
        {
            var relativeLayoutPath = (await expression.EvaluateAsync(context)).ToStringValue();
            if (!relativeLayoutPath.EndsWith(FluidViewEngine.ViewExtension, StringComparison.OrdinalIgnoreCase))
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
