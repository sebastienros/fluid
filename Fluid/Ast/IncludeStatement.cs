using Fluid.Values;
using System.Text.Encodings.Web;

namespace Fluid.Ast
{
#pragma warning disable CA1001 // Types that own disposable fields should be disposable
    public sealed class IncludeStatement : Statement
#pragma warning restore CA1001
    {
        public const string ViewExtension = ".liquid";

        public IncludeStatement(FluidParser parser, Expression path, Expression with = null, Expression @for = null, string alias = null, IReadOnlyList<AssignStatement> assignStatements = null)
        {
            Parser = parser;
            Path = path;
            With = with;
            For = @for;
            Alias = alias;
            AssignStatements = assignStatements ?? [];
        }

        public FluidParser Parser { get; }
        public Expression Path { get; }
        public IReadOnlyList<AssignStatement> AssignStatements { get; }
        public Expression With { get; }
        public Expression For { get; }
        public string Alias { get; }

        public override async ValueTask<Completion> WriteToAsync(IFluidOutput output, TextEncoder encoder, TemplateContext context)
        {
            context.IncrementSteps();

            var relativePath = (await Path.EvaluateAsync(context)).ToStringValue();
            var loadedTemplate = await TemplateLoader.LoadAsync(
                Parser,
                relativePath,
                context,
                context.Options.DefaultFileExtension);
            relativePath = loadedTemplate.Path;
            var template = loadedTemplate.Template;

            var identifier = System.IO.Path.GetFileNameWithoutExtension(relativePath);

            // Unlike render, include shares scope with the parent template.
            // Use a for-loop scope which passes through variable assignments to the parent.
            // This allows variables assigned inside the include to persist in the outer scope.
            context.EnterForLoopScope();

            // Track keyword argument names so we can clean them up after
            List<string> keywordArgNames = null;

            try
            {
                if (With != null)
                {
                    var with = await With.EvaluateAsync(context);

                    // The bound variable is local to this include
                    context.LocalScope.SetOwnValue(Alias ?? identifier, with);

                    // Keyword arguments are local to the include
                    if (AssignStatements.Count > 0)
                    {
                        keywordArgNames = new List<string>(AssignStatements.Count);
                        for (var i = 0; i < AssignStatements.Count; i++)
                        {
                            var stmt = AssignStatements[i];
                            keywordArgNames.Add(stmt.Identifier);
                            context.LocalScope.SetOwnValue(stmt.Identifier, await stmt.Value.EvaluateAsync(context));
                        }
                    }

                    return await RenderStatementsAsync(template, output, encoder, context);
                }
                else if (AssignStatements.Count > 0)
                {
                    // Keyword arguments are local to the include - they should go out of scope after
                    keywordArgNames = new List<string>(AssignStatements.Count);
                    for (var i = 0; i < AssignStatements.Count; i++)
                    {
                        var stmt = AssignStatements[i];
                        keywordArgNames.Add(stmt.Identifier);
                        context.LocalScope.SetOwnValue(stmt.Identifier, await stmt.Value.EvaluateAsync(context));
                    }

                    return await RenderStatementsAsync(template, output, encoder, context);
                }
                else if (For != null)
                {
                    try
                    {
                        var forloop = new ForLoopValue();

                        var evaluatedFor = await For.EvaluateAsync(context);

                        // Fast-path: avoid re-enumerating already materialized arrays.
                        IReadOnlyList<FluidValue> list = evaluatedFor is ArrayValue array
                            ? array.Values
                            : await evaluatedFor.EnumerateAsync(context).ToListAsync(context.CancellationToken);

                        var length = forloop.Length = list.Count;

                        context.LocalScope.SetOwnValue("forloop", forloop);

                        for (var i = 0; i < length; i++)
                        {
                            context.IncrementSteps();

                            var item = list[i];

                            context.LocalScope.SetOwnValue(Alias ?? identifier, item);

                            // Set helper variables
                            forloop.Index = i + 1;
                            forloop.Index0 = i;
                            forloop.RIndex = length - i;
                            forloop.RIndex0 = length - i - 1;
                            forloop.First = i == 0;
                            forloop.Last = i == length - 1;

                            var completion = await RenderStatementsAsync(template, output, encoder, context);

                            if (completion == Completion.Break)
                            {
                                break;
                            }

                            // Restore the forloop property after every statement in case it replaced it,
                            // for instance if it contains a nested for loop
                            context.LocalScope.SetOwnValue("forloop", forloop);
                        }
                    }
                    finally
                    {
                        context.LocalScope.DeleteOwn("forloop");
                    }

                    return Completion.Normal;
                }
                else
                {
                    // no with, for or assignments, e.g. {% include 'products' %}
                    return await RenderStatementsAsync(template, output, encoder, context);
                }
            }
            finally
            {
                // Clean up keyword arguments from local scope
                if (keywordArgNames != null)
                {
                    foreach (var name in keywordArgNames)
                    {
                        context.LocalScope.DeleteOwn(name);
                    }
                }

                context.ReleaseScope();
            }
        }

        /// <summary>
        /// Renders template statements and returns the completion status.
        /// This allows break/continue signals to propagate from included templates.
        /// </summary>
        private static async ValueTask<Completion> RenderStatementsAsync(IFluidTemplate template, IFluidOutput output, TextEncoder encoder, TemplateContext context)
        {
            if (template is IStatementList statementList)
            {
                var statements = statementList.Statements;
                var count = statements.Count;
                for (var i = 0; i < count; i++)
                {
                    var completion = await statements[i].WriteToAsync(output, encoder, context);

                    if (completion != Completion.Normal)
                    {
                        return completion;
                    }
                }
            }
            else
            {
                // Fallback for non-standard template implementations
                await template.RenderAsync(output, encoder, context);
            }

            context.CancellationToken.ThrowIfCancellationRequested();
            await output.FlushAsync();
            return Completion.Normal;
        }

        protected internal override Statement Accept(AstVisitor visitor) => visitor.VisitIncludeStatement(this);

        private sealed record CachedTemplate(IFluidTemplate Template, string Name);
    }
}
