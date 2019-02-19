using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Fluid.Ast
{
    public class IncludeStatement : Statement
    {
        public const string ViewExtension = ".liquid";

        public IncludeStatement(Expression path, Expression with = null, IList<AssignStatement> assignStatements = null)
        {
            Path = path;
            With = with;
            AssignStatements = assignStatements;
        }

        public Expression Path { get; }

        public IList<AssignStatement> AssignStatements { get; }

        public Expression With { get; }

        public override async Task<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            var relativePath = (await Path.EvaluateAsync(context)).ToStringValue();
            if (!relativePath.EndsWith(ViewExtension, StringComparison.OrdinalIgnoreCase))
            {
                relativePath += ViewExtension;
            }

            var fileProvider = context.FileProvider ?? TemplateContext.GlobalFileProvider;
            var fileInfo = fileProvider.GetFileInfo(relativePath);

            if (!fileInfo.Exists)
            {
                throw new FileNotFoundException(relativePath);
            }

            using (var stream = fileInfo.CreateReadStream())
            using (var streamReader = new StreamReader(stream))
            {
                context.EnterChildScope();

                string partialTemplate = await streamReader.ReadToEndAsync();
                var parser = CreateParser(context);
                if (parser.TryParse(partialTemplate, true, out var statements, out var errors))
                {
                    var template = CreateTemplate(context, statements);
                    if (With != null)
                    {
                        var identifier = System.IO.Path.GetFileNameWithoutExtension(relativePath);
                        var with = await With.EvaluateAsync(context);
                        context.SetValue(identifier, with);
                    }

                    if (AssignStatements != null)
                    {
                        foreach (var assignStatement in AssignStatements)
                        {
                            await assignStatement.WriteToAsync(writer, encoder, context);
                        }
                    }

                    await template.RenderAsync(writer, encoder, context);
                }
                else
                {
                    throw new Exception(String.Join(Environment.NewLine, errors));
                }

                context.ReleaseScope();
            }

            return Completion.Normal;
        }

        private static IFluidParser CreateParser(TemplateContext context)
        {
            return context.ParserFactory != null
                ? context.ParserFactory.CreateParser()
                : FluidTemplate.Factory.CreateParser()
                ;
        }

        private static IFluidTemplate CreateTemplate(TemplateContext context, List<Statement> statements)
        {
            IFluidTemplate template = context.TemplateFactory != null 
                ? context.TemplateFactory()
                : new FluidTemplate()
                ;

            template.Statements = statements;
            return template;
        }
    }
}
