using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Fluid.Ast
{
    public class IncludeStatement : Statement
    {
        /// <summary>
        /// Override <see cref="IFluidParserFactory"/> used by a <see cref="TemplateContext"/>
        /// by setting an ambient value with a key matching the value of the
        /// <see cref="FluidParserFactoryKey"/>.
        /// </summary>
        public const string FluidParserFactoryKey = "FluidParserFactory";
        /// <summary>
        /// Override <see cref="Func{IFluidTemplate}"/> used by a <see cref="TemplateContext"/>
        /// by setting an ambient value with a key matching the value of the
        /// <see cref="FluidTemplateFactoryKey"/>.
        /// </summary>
        public const string FluidTemplateFactoryKey = "FluidTemplateFactory";
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
                if (parser.TryParse(partialTemplate, out var statements, out var errors))
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
            if (context.AmbientValues.TryGetValue(FluidParserFactoryKey, out var factory))
            {
                return ((IFluidParserFactory)factory).CreateParser();
            }
            else
            {
                return FluidTemplate.Factory.CreateParser();
            }
        }

        private static IFluidTemplate CreateTemplate(TemplateContext context, IList<Statement> statements)
        {
            IFluidTemplate template;
            if (context.AmbientValues.TryGetValue(FluidTemplateFactoryKey, out var factory))
            {
                template = ((Func<IFluidTemplate>)factory)();
            }
            else
            {
                template = new FluidTemplate();
            }
            template.Statements = statements;
            return template;
        }
    }
}
