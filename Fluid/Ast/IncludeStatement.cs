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
        private readonly IFluidParser _parser;

        public IncludeStatement(IFluidParser parser, Expression path, Expression with = null, IList<AssignStatement> assignStatements = null)
        {
            _parser = parser;
            Path = path;
            With = with;
            AssignStatements = assignStatements;
        }

        public Expression Path { get; }

        public IList<AssignStatement> AssignStatements { get; }

        public Expression With { get; }

        public override async ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            context.IncrementSteps();

            var relativePath = (await Path.EvaluateAsync(context)).ToStringValue();
            if (!relativePath.EndsWith(ViewExtension, StringComparison.OrdinalIgnoreCase))
            {
                relativePath += ViewExtension;
            }

            var fileProvider = context.FileProvider ?? TemplateContext.GlobalFileProvider;
            var fileInfo = fileProvider.GetFileInfo(relativePath);

            if (fileInfo == null || !fileInfo.Exists)
            {
                throw new FileNotFoundException(relativePath);
            }

            using (var stream = fileInfo.CreateReadStream())
            using (var streamReader = new StreamReader(stream))
            {
                context.EnterChildScope();

                string partialTemplate = await streamReader.ReadToEndAsync();

                if (_parser.TryParse(partialTemplate, out var result, out var errors))
                {
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

                    await result.RenderAsync(writer, encoder, context);
                }
                else
                {
                    throw new ParseException(errors);
                }

                context.ReleaseScope();
            }

            return Completion.Normal;
        }
    }
}
