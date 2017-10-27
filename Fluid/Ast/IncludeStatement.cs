using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Fluid.Ast
{
    public class IncludeStatement : Statement
    {
        public const string FluidParserFactoryKey = "FluidParserFactory";
        public const string FluidTemplateKey = "FluidTemplate";
        public const string ViewExtension = ".liquid";

        public IncludeStatement(Expression path)
        {
            Path = path;
        }

        public Expression Path { get; }

        public override async Task<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            var relativePath = (await Path.EvaluateAsync(context)).ToStringValue();
            if (!relativePath.EndsWith(ViewExtension))
            {
                relativePath += ViewExtension;
            }

            var fileProvider = context.FileProvider ?? TemplateContext.GlobalFileProvider;
            var fileInfo = fileProvider.GetFileInfo(relativePath);

            if (!fileInfo.Exists)
            {
                throw new FileNotFoundException(relativePath);
            }

            if (!context.AmbientValues.ContainsKey(FluidParserFactoryKey))
            {
                throw new ArgumentException($"The '{FluidParserFactoryKey}' key was not present in the AmbientValues dictionary.", nameof(context));
            }
            if (!context.AmbientValues.ContainsKey(FluidTemplateKey))
            {
                throw new ArgumentException($"The '{FluidTemplateKey}' key was not present in the AmbientValues dictionary.", nameof(context));
            }

            FluidParserFactory factory = (FluidParserFactory)context.AmbientValues[FluidParserFactoryKey];
            IFluidParser parser = factory.CreateParser();
            IFluidTemplate template = (IFluidTemplate)context.AmbientValues[FluidTemplateKey];

            using (var stream = fileInfo.CreateReadStream())
            using (var streamReader = new StreamReader(stream))
            {
                var childScope = context.EnterChildScope();

                string partialTemplate = await streamReader.ReadToEndAsync();
                if (parser.TryParse(partialTemplate, out var statements, out var errors))
                {
                    template.Statements = statements;
                    await template.RenderAsync(writer, encoder, context);
                }
                else
                {
                    throw new Exception(String.Join(Environment.NewLine, errors));
                }

                childScope.ReleaseScope();
            }

            return Completion.Normal;
        }
    }
}
