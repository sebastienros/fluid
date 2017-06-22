using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Fluid.Ast
{
    public class IncludeStatement : Statement
    {
        public IncludeStatement(Expression path)
        {
            Path = path;
        }

        public Expression Path { get; }

        public override async Task<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            var relativePath = (await Path.EvaluateAsync(context)).ToStringValue();
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
