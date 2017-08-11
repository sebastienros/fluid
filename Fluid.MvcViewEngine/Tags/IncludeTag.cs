using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Fluid.Ast;
using Fluid.Tags;
using FluidMvcViewEngine;

namespace Fluid.MvcViewEngine.Tags
{
    public class IncludeTag : ExpressionTag
    {
        public override async Task<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context, Expression path)
        {
            var relativePath = (await path.EvaluateAsync(context)).ToStringValue();
            if (!relativePath.EndsWith(FluidViewEngine.ViewExtension))
            {
                relativePath += FluidViewEngine.ViewExtension;
            }

            var fileProvider = context.FileProvider ?? TemplateContext.GlobalFileProvider;
            var fileInfo = fileProvider.GetFileInfo(relativePath);
            string partialContent;

            using (var stream = fileInfo.CreateReadStream())
            using (var streamReader = new StreamReader(stream))
            {
                partialContent = await streamReader.ReadToEndAsync();
            }

            await writer.WriteAsync(partialContent);

            return Completion.Normal;
        }
    }
}
