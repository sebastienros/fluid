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
        private readonly FluidParser _parser;
        private IFluidTemplate _template;
        private string _identifier;

        public IncludeStatement(FluidParser parser, Expression path, Expression with = null, IList<AssignStatement> assignStatements = null)
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

            if (_template == null || !string.Equals(_identifier, System.IO.Path.GetFileNameWithoutExtension(relativePath), StringComparison.OrdinalIgnoreCase))
            {
                var fileProvider = context.Options.FileProvider;

                var fileInfo = fileProvider.GetFileInfo(relativePath);

                if (fileInfo == null || !fileInfo.Exists)
                {
                    throw new FileNotFoundException(relativePath);
                }

                var content = "";

                using (var stream = fileInfo.CreateReadStream())
                using (var streamReader = new StreamReader(stream))
                {
                    content = await streamReader.ReadToEndAsync();
                }

                if (!_parser.TryParse(content, out _template, out var errors))
                {
                    throw new ParseException(errors);
                }

                _identifier = System.IO.Path.GetFileNameWithoutExtension(relativePath);
            }

            try
            {
                context.EnterChildScope();

                if (With != null)
                {
                    var with = await With.EvaluateAsync(context);
                    context.SetValue(_identifier, with);
                }

                if (AssignStatements != null)
                {
                    foreach (var assignStatement in AssignStatements)
                    {
                        await assignStatement.WriteToAsync(writer, encoder, context);
                    }
                }

                await _template.RenderAsync(writer, encoder, context);
            }
            finally
            {
                context.ReleaseScope();
            }

            return Completion.Normal;
        }
    }
}
