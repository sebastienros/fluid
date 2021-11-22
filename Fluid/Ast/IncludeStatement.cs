using Fluid.Values;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        public IncludeStatement(FluidParser parser, Expression path, Expression with = null, Expression @for = null, string alias = null, IList<AssignStatement> assignStatements = null, bool isolatedScope = false)
        {
            _parser = parser;
            IsolatedScope = isolatedScope;
            Path = path;
            With = with;
            For = @for;
            Alias = alias;
            AssignStatements = assignStatements;
        }

        public bool IsolatedScope { get; }
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

            try
            {
                if (IsolatedScope)
                {
                    // render tag
                    context.EnterIsolatedScope();
                }
                else
                {
                    //include tag
                    context.EnterChildScope(); 
                }

                if (With != null)
                {
                    var with = await With.EvaluateAsync(context);
                    context.SetValue(Alias ?? _identifier, with);

                    await _template.RenderAsync(writer, encoder, context);
                }
                else if (AssignStatements != null)
                {
                    foreach (var assignStatement in AssignStatements)
                    {
                        await assignStatement.WriteToAsync(writer, encoder, context);
                    }

                    await _template.RenderAsync(writer, encoder, context);
                }
                else if (For != null)
                {
                    try
                    {
                        var forloop = new ForLoopValue();

                        var list = (await For.EvaluateAsync(context)).Enumerate(context).ToList();

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
                    // no with, for or assignments, e.g. {% include 'products' %}
                    await _template.RenderAsync(writer, encoder, context);
                }
            }
            finally
            {
                context.ReleaseScope();

                // use this in render tag
                // context.LocalScope = new Scope(context.Options.Scope);

            }

            return Completion.Normal;
        }
    }
}
