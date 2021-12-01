using Fluid.Values;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Fluid.Ast
{
    /// <summary>
    /// The render tag can only access immutable environments, which means the scope of the context that was passed to the main template, the options' scope, and the model.
    /// </summary>
    public class RenderStatement : Statement
    {
        public const string ViewExtension = ".liquid";
        private readonly FluidParser _parser;
        private IFluidTemplate _template;
        private string _identifier;

        public RenderStatement(FluidParser parser, Expression path, Expression with = null, Expression @for = null, string alias = null, IList<AssignStatement> assignStatements = null)
        {
            _parser = parser;
            Path = path;
            With = with;
            For = @for;
            Alias = alias;
            AssignStatements = assignStatements;
        }

        public Expression Path { get; }
        public IList<AssignStatement> AssignStatements { get; }
        public Expression With { get; }
        public Expression For { get; }
        public string Alias { get; }

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

            context.EnterChildScope();
            var previousScope = context.LocalScope;
            
            try
            {
                if (With != null)
                {
                    var with = await With.EvaluateAsync(context);

                    context.LocalScope = new Scope(context.RootScope);
                    previousScope.CopyTo(context.LocalScope);

                    context.SetValue(Alias ?? _identifier, with);
                    await _template.RenderAsync(writer, encoder, context);
                }
                else if (AssignStatements != null)
                {
                    foreach (var assignStatement in AssignStatements)
                    {
                        await assignStatement.WriteToAsync(writer, encoder, context);
                    }

                    context.LocalScope = new Scope(context.RootScope);
                    previousScope.CopyTo(context.LocalScope);

                    await _template.RenderAsync(writer, encoder, context);
                }
                else if (For != null)
                {
                    try
                    {
                        var forloop = new ForLoopValue();

                        var list = (await For.EvaluateAsync(context)).Enumerate(context).ToList();

                        context.LocalScope = new Scope(context.RootScope);
                        previousScope.CopyTo(context.LocalScope);

                        var length = forloop.Length = list.Count;

                        context.SetValue("forloop", forloop);

                        for (var i = 0; i < length; i++)
                        {
                            context.IncrementSteps();

                            var item = list[i];

                            context.SetValue(Alias ?? _identifier, item);

                            // Set helper variables
                            forloop.Index = i + 1;
                            forloop.Index0 = i;
                            forloop.RIndex = length - i - 1;
                            forloop.RIndex0 = length - i;
                            forloop.First = i == 0;
                            forloop.Last = i == length - 1;

                            await _template.RenderAsync(writer, encoder, context);

                            // Restore the forloop property after every statement in case it replaced it,
                            // for instance if it contains a nested for loop
                            context.SetValue("forloop", forloop);
                        }
                    }
                    finally
                    {
                        context.LocalScope.Delete("forloop");
                    }
                }
                else
                {
                    context.LocalScope = new Scope(context.RootScope);
                    previousScope.CopyTo(context.LocalScope);

                    await _template.RenderAsync(writer, encoder, context);
                }
            }
            finally
            {
                context.LocalScope = previousScope;
                context.ReleaseScope();
            }

            return Completion.Normal;
        }
    }
}
