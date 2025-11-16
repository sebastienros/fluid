using Fluid.Values;
using System.Text.Encodings.Web;

namespace Fluid.Ast
{
    /// <summary>
    /// The render tag can only access immutable environments, which means the scope of the context that was passed to the main template, the options' scope, and the model.
    /// </summary>
#pragma warning disable CA1001 // Types that own disposable fields should be disposable
    public sealed class RenderStatement : Statement
#pragma warning restore CA1001
    {
        public const string ViewExtension = ".liquid";

        public RenderStatement(FluidParser parser, string path, Expression with = null, Expression @for = null, string alias = null, IReadOnlyList<AssignStatement> assignStatements = null)
        {
            Parser = parser;
            Path = path;
            With = with;
            For = @for;
            Alias = alias;
            AssignStatements = assignStatements ?? [];
        }

        public FluidParser Parser { get; }
        public string Path { get; }
        public IReadOnlyList<AssignStatement> AssignStatements { get; }
        public Expression With { get; }
        public Expression For { get; }
        public string Alias { get; }

        public override async ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            context.IncrementSteps();

            var relativePath = Path;
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

                context.Options.TemplateCache?.SetTemplate(relativePath, fileInfo.LastModified, template);
            }

            var identifier = System.IO.Path.GetFileNameWithoutExtension(relativePath);

            context.EnterChildScope();
            var previousScope = context.LocalScope;

            try
            {
                if (With != null)
                {
                    var with = await With.EvaluateAsync(context);

                    context.LocalScope = new Scope(context.RootScope);
                    previousScope.CopyTo(context.LocalScope);

                    context.SetValue(Alias ?? identifier, with);

                    // Evaluate assign statements in the new scope if present
                    if (AssignStatements.Count > 0)
                    {
                        var length = AssignStatements.Count;
                        for (var i = 0; i < length; i++)
                        {
                            await AssignStatements[i].WriteToAsync(writer, encoder, context);
                        }
                    }

                    await template.RenderAsync(writer, encoder, context);
                }
                else if (For != null)
                {
                    try
                    {
                        var forloop = new ForLoopValue();

                        var list = await (await For.EvaluateAsync(context)).EnumerateAsync(context).ToListAsync();

                        context.LocalScope = new Scope(context.RootScope);
                        previousScope.CopyTo(context.LocalScope);

                        // Evaluate assign statements in the new scope before the loop if present
                        if (AssignStatements.Count > 0)
                        {
                            var assignLength = AssignStatements.Count;
                            for (var j = 0; j < assignLength; j++)
                            {
                                await AssignStatements[j].WriteToAsync(writer, encoder, context);
                            }
                        }

                        var length = forloop.Length = list.Count;

                        context.SetValue("forloop", forloop);

                        for (var i = 0; i < length; i++)
                        {
                            context.IncrementSteps();

                            var item = list[i];

                            context.SetValue(Alias ?? identifier, item);

                            // Set helper variables
                            forloop.Index = i + 1;
                            forloop.Index0 = i;
                            forloop.RIndex = length - i;
                            forloop.RIndex0 = length - i - 1;
                            forloop.First = i == 0;
                            forloop.Last = i == length - 1;

                            await template.RenderAsync(writer, encoder, context);

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
                else if (AssignStatements.Count > 0)
                {
                    var length = AssignStatements.Count;
                    for (var i = 0; i < length; i++)
                    {
                        await AssignStatements[i].WriteToAsync(writer, encoder, context);
                    }

                    context.LocalScope = new Scope(context.RootScope);
                    previousScope.CopyTo(context.LocalScope);

                    await template.RenderAsync(writer, encoder, context);
                }
                else
                {
                    context.LocalScope = new Scope(context.RootScope);
                    previousScope.CopyTo(context.LocalScope);

                    await template.RenderAsync(writer, encoder, context);
                }
            }
            finally
            {
                context.LocalScope = previousScope;
                context.ReleaseScope();
            }

            return Completion.Normal;
        }

        protected internal override Statement Accept(AstVisitor visitor) => visitor.VisitRenderStatement(this);

        private sealed record CachedTemplate(IFluidTemplate Template, string Name);
    }
}
