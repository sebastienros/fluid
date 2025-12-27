using Fluid.Values;
using System.Text.Encodings.Web;
using Fluid.SourceGeneration;

namespace Fluid.Ast
{
    /// <summary>
    /// The render tag can only access immutable environments, which means the scope of the context that was passed to the main template, the options' scope, and the model.
    /// </summary>
#pragma warning disable CA1001 // Types that own disposable fields should be disposable
    public sealed class RenderStatement : Statement, ISourceable
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

                // Allow user to modify the template before caching (e.g., apply visitors/rewriters)
                if (context.Options.TemplateParsed != null)
                {
                    template = context.Options.TemplateParsed(relativePath, template);
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

        public void WriteTo(SourceGenerationContext context)
        {
            // The referenced template is compiled ahead-of-time and resolved by path.
            var templateTypeName = context.GetRenderTemplateTypeName(Path);

            context.WriteLine($"{context.ContextName}.IncrementSteps();");
            context.WriteLine($"var template = new {templateTypeName}();");

            // Use the same default identifier logic as runtime (file name without extension).
            context.WriteLine($"var identifier = System.IO.Path.GetFileNameWithoutExtension({SourceGenerationContext.ToCSharpStringLiteral(Path)});");

            context.WriteLine($"{context.ContextName}.EnterChildScope();");
            context.WriteLine($"var previousScope = {context.ContextName}.LocalScope;");

            context.WriteLine("try");
            context.WriteLine("{");
            using (context.Indent())
            {
                if (With != null)
                {
                    var withExpr = context.GetExpressionMethodName(With);
                    context.WriteLine($"var withValue = await {withExpr}({context.ContextName});");

                    context.WriteLine($"{context.ContextName}.LocalScope = new Scope({context.ContextName}.Options.Scope, false, {context.ContextName}.ModelNamesComparer);");
                    context.WriteLine($"previousScope.CopyTo({context.ContextName}.LocalScope);");

                    if (!string.IsNullOrEmpty(Alias))
                    {
                        context.WriteLine($"{context.ContextName}.SetValue({SourceGenerationContext.ToCSharpStringLiteral(Alias)}, withValue);");
                    }
                    else
                    {
                        context.WriteLine($"{context.ContextName}.SetValue(identifier, withValue);");
                    }

                    if (AssignStatements.Count > 0)
                    {
                        for (var i = 0; i < AssignStatements.Count; i++)
                        {
                            var assignStmt = context.GetStatementMethodName(AssignStatements[i]);
                            context.WriteLine($"await {assignStmt}({context.WriterName}, {context.EncoderName}, {context.ContextName});");
                        }
                    }

                    context.WriteLine($"await template.RenderAsync({context.WriterName}, {context.EncoderName}, {context.ContextName});");
                }
                else if (For != null)
                {
                    var forExpr = context.GetExpressionMethodName(For);

                    context.WriteLine("try");
                    context.WriteLine("{");
                    using (context.Indent())
                    {
                        context.WriteLine("var forloop = new ForLoopValue();");
                        context.WriteLine($"var list = await (await {forExpr}({context.ContextName})).EnumerateAsync({context.ContextName}).ToListAsync();");

                        context.WriteLine($"{context.ContextName}.LocalScope = new Scope({context.ContextName}.Options.Scope, false, {context.ContextName}.ModelNamesComparer);");
                        context.WriteLine($"previousScope.CopyTo({context.ContextName}.LocalScope);");

                        if (AssignStatements.Count > 0)
                        {
                            for (var j = 0; j < AssignStatements.Count; j++)
                            {
                                var assignStmt = context.GetStatementMethodName(AssignStatements[j]);
                                context.WriteLine($"await {assignStmt}({context.WriterName}, {context.EncoderName}, {context.ContextName});");
                            }
                        }

                        context.WriteLine("var length = forloop.Length = list.Count;");
                        context.WriteLine($"{context.ContextName}.SetValue(\"forloop\", forloop);");

                        context.WriteLine("for (var i = 0; i < length; i++)");
                        context.WriteLine("{");
                        using (context.Indent())
                        {
                            context.WriteLine($"{context.ContextName}.IncrementSteps();");
                            context.WriteLine("var item = list[i];");

                            if (!string.IsNullOrEmpty(Alias))
                            {
                                context.WriteLine($"{context.ContextName}.SetValue({SourceGenerationContext.ToCSharpStringLiteral(Alias)}, item);");
                            }
                            else
                            {
                                context.WriteLine($"{context.ContextName}.SetValue(identifier, item);");
                            }

                            context.WriteLine("forloop.Index = i + 1;");
                            context.WriteLine("forloop.Index0 = i;");
                            context.WriteLine("forloop.RIndex = length - i;");
                            context.WriteLine("forloop.RIndex0 = length - i - 1;");
                            context.WriteLine("forloop.First = i == 0;");
                            context.WriteLine("forloop.Last = i == length - 1;");

                            context.WriteLine($"await template.RenderAsync({context.WriterName}, {context.EncoderName}, {context.ContextName});");
                            context.WriteLine($"{context.ContextName}.SetValue(\"forloop\", forloop);");
                        }
                        context.WriteLine("}");
                    }
                    context.WriteLine("}");
                    context.WriteLine("finally");
                    context.WriteLine("{");
                    using (context.Indent())
                    {
                        context.WriteLine($"{context.ContextName}.LocalScope.Delete(\"forloop\");");
                    }
                    context.WriteLine("}");
                }
                else if (AssignStatements.Count > 0)
                {
                    for (var i = 0; i < AssignStatements.Count; i++)
                    {
                        var assignStmt = context.GetStatementMethodName(AssignStatements[i]);
                        context.WriteLine($"await {assignStmt}({context.WriterName}, {context.EncoderName}, {context.ContextName});");
                    }

                    context.WriteLine($"{context.ContextName}.LocalScope = new Scope({context.ContextName}.Options.Scope, false, {context.ContextName}.ModelNamesComparer);");
                    context.WriteLine($"previousScope.CopyTo({context.ContextName}.LocalScope);");
                    context.WriteLine($"await template.RenderAsync({context.WriterName}, {context.EncoderName}, {context.ContextName});");
                }
                else
                {
                    context.WriteLine($"{context.ContextName}.LocalScope = new Scope({context.ContextName}.Options.Scope, false, {context.ContextName}.ModelNamesComparer);");
                    context.WriteLine($"previousScope.CopyTo({context.ContextName}.LocalScope);");
                    context.WriteLine($"await template.RenderAsync({context.WriterName}, {context.EncoderName}, {context.ContextName});");
                }
            }
            context.WriteLine("}");
            context.WriteLine("finally");
            context.WriteLine("{");
            using (context.Indent())
            {
                context.WriteLine($"{context.ContextName}.LocalScope = previousScope;");
                context.WriteLine($"{context.ContextName}.ReleaseScope();");
            }
            context.WriteLine("}");

            context.WriteLine("return Completion.Normal;");
        }

        private sealed record CachedTemplate(IFluidTemplate Template, string Name);
    }
}
