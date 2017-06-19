using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Fluid.Ast;
using FluidMvcViewEngine;
using Microsoft.Extensions.FileProviders;

namespace Fluid.MvcViewEngine.Statements
{
    public class LayoutStatement : Statement
    {
        public LayoutStatement(Expression path)
        {
            Path = path;
        }

        public Expression Path { get; }

        public override async Task<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            var relativeLayoutPath = (await Path.EvaluateAsync(context)).ToStringValue();
            var currentViewPath = context.AmbientValues[FluidRendering.ViewPath] as string;
            var currentDirectory = System.IO.Path.GetDirectoryName(currentViewPath);
            var layoutPath = System.IO.Path.Combine(currentDirectory, relativeLayoutPath);
            
            context.AmbientValues["Layout"] = layoutPath;

            return Completion.Normal;
        }
    }
}
