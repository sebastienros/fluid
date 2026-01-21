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
            var fileProvider = context.Options.FileProvider;

            // First, try to get the file with the exact path provided
            var fileInfo = fileProvider.GetFileInfo(relativePath);

            // If the file doesn't exist and a default extension is configured
            if ((fileInfo == null || !fileInfo.Exists || fileInfo.IsDirectory) && !string.IsNullOrEmpty(context.Options.DefaultFileExtension))
            {
                // Check if the path already ends with the default extension
                if (!relativePath.EndsWith(context.Options.DefaultFileExtension, StringComparison.OrdinalIgnoreCase))
                {
                    // Try adding the default extension
                    var pathWithExtension = relativePath + context.Options.DefaultFileExtension;
                    var fileInfoWithExtension = fileProvider.GetFileInfo(pathWithExtension);

                    if (fileInfoWithExtension != null && fileInfoWithExtension.Exists && !fileInfoWithExtension.IsDirectory)
                    {
                        relativePath = pathWithExtension;
                        fileInfo = fileInfoWithExtension;
                    }
                }
            }

            if (fileInfo == null || !fileInfo.Exists || fileInfo.IsDirectory)
            {
                throw new FileNotFoundException(relativePath);
            }

            if (context.Options.TemplateCache == null || !context.Options.TemplateCache.TryGetTemplate(relativePath, fileInfo.LastModified, out var template))
            {
                var content = "";

                using (var stream = fileInfo.CreateReadStream())
                using (var streamReader = new StreamReader(stream))
                {
                    content = await streamReader.ReadToEndAsync();
                }

                if (!Parser.TryParse(content, out template, out var errors))
                {
                    throw new ParseException(errors);
                }

                // Allow user to modify the template before caching (e.g., apply visitors/rewriters)
                if (context.Options.TemplateParsed != null)
                {
                    template = context.Options.TemplateParsed(relativePath, template);
                }

                context.Options.TemplateCache?.SetTemplate(relativePath, fileInfo.LastModified, template);
            }

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
                            : await evaluatedFor.EnumerateAsync(context).ToListAsync();

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

            await output.FlushAsync();
            return Completion.Normal;
        }

        protected internal override Statement Accept(AstVisitor visitor) => visitor.VisitIncludeStatement(this);

        private sealed record CachedTemplate(IFluidTemplate Template, string Name);
    }
}
