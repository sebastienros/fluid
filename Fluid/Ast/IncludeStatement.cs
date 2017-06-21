using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.Extensions.FileProviders;

namespace Fluid.Ast
{
    public class IncludeStatement : Statement
    {
        private static readonly IFileProvider _blankFileProvider = new NullFileProvider();

        public IncludeStatement(Expression path)
        {
            Path = path;
        }

        public Expression Path { get; }

        public override async Task<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            var relativePath = (await Path.EvaluateAsync(context)).ToStringValue();
            var fileProvider = context.FileProvider ?? _blankFileProvider;
            var fileInfo = fileProvider.GetFileInfo(relativePath);
            string partialContent;

            using (var stream = fileInfo.CreateReadStream())
            using (var streamReader = new StreamReader(stream))
            {
                partialContent = await streamReader.ReadToEndAsync();
            }

            await writer.WriteAsync(partialContent);

            return Completion.Continue;
        }
    }
}
