using Fluid.SourceGeneration;
using Fluid.Values;
using System.Text.Encodings.Web;

namespace Fluid.Ast;

#pragma warning disable CA1001 // Types that own disposable fields should be disposable
public sealed class IncludeStatement : Statement, ISourceable
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

        var relativePath = (await Path.EvaluateAsync(context)).ToStringValue(context);
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
        using var scope = context.EnterScope(ScopeBehavior.WriteThrough);

        if (With != null)
        {
            var with = await With.EvaluateAsync(context);

            // The bound variable is local to this include
            context.LocalScope.SetOwnValue(Alias ?? identifier, with);

            // Keyword arguments are local to the include
            if (AssignStatements.Count > 0)
            {
                for (var i = 0; i < AssignStatements.Count; i++)
                {
                    var stmt = AssignStatements[i];
                    context.LocalScope.SetOwnValue(stmt.Identifier, await stmt.Value.EvaluateAsync(context));
                }
            }

            return await RenderStatementsAsync(template, output, encoder, context);
        }
        else if (For != null)
        {
            var forloop = ForLoopValue.Create();

            var evaluatedFor = await For.EvaluateAsync(context);

            var list = await ArrayValue.ToFluidArrayAsync(evaluatedFor, context);

            var length = forloop.Length = list.Count;

            context.LocalScope.SetOwnValue("forloop", forloop);

            // Keyword arguments are local to the include
            for (var i = 0; i < AssignStatements.Count; i++)
            {
                var stmt = AssignStatements[i];
                context.LocalScope.SetOwnValue(stmt.Identifier, await stmt.Value.EvaluateAsync(context));
            }

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

            return Completion.Normal;
        }

        // Keyword arguments alone, or nothing, e.g. {% include 'products' %}.
        // Keyword arguments are local to the include - they should go out of scope after
        for (var i = 0; i < AssignStatements.Count; i++)
        {
            var stmt = AssignStatements[i];
            context.LocalScope.SetOwnValue(stmt.Identifier, await stmt.Value.EvaluateAsync(context));
        }

        return await RenderStatementsAsync(template, output, encoder, context);
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

        // The render this include is part of flushes the output when it ends.
        if (!context.IsFlushedByEnclosingRender(output))
        {
            await output.FlushAsync();
        }
        return Completion.Normal;
    }

    protected internal override Statement Accept(AstVisitor visitor) => visitor.VisitIncludeStatement(this);

    public void WriteTo(SourceGenerationContext context)
    {
        if (!SourceGenerationContext.TryGetLiteralString(Path, out var path))
        {
            throw new SourceGenerationException("An 'include' tag with a dynamic path is not compatible with source generation.");
        }

        // The included template is compiled ahead-of-time and writes in this template's scope and output.
        var templateTypeName = context.GetRenderTemplateTypeName(path);
        var ctx = context.ContextName;
        var call = $"await {templateTypeName}.WriteStatementsAsync({context.WriterName}, {context.EncoderName}, {ctx})";
        var variableName = SourceGenerationContext.ToCSharpStringLiteral(Alias ?? System.IO.Path.GetFileNameWithoutExtension(path));

        void EmitAssignStatements()
        {
            for (var i = 0; i < AssignStatements.Count; i++)
            {
                var assignStatement = AssignStatements[i];
                var valueExpr = context.GetExpressionMethodName(assignStatement.Value);
                context.WriteLine($"{ctx}.LocalScope.SetOwnValue({SourceGenerationContext.ToCSharpStringLiteral(assignStatement.Identifier)}, await {valueExpr}({ctx}));");
            }
        }

        context.WriteLine($"{ctx}.IncrementSteps();");
        context.WriteLine($"using var scope = {ctx}.EnterScope(ScopeBehavior.WriteThrough);");

        if (With != null)
        {
            var withExpr = context.GetExpressionMethodName(With);
            context.WriteLine($"{ctx}.LocalScope.SetOwnValue({variableName}, await {withExpr}({ctx}));");
            EmitAssignStatements();
            context.WriteLine($"return {call};");
        }
        else if (For != null)
        {
            // Nothing reads the forloop variable of most loops, which then don't create or update it.
            var usesForLoop = context.UsesForLoop(this);

            var forExpr = context.GetExpressionMethodName(For);
            context.WriteLine($"var list = await ArrayValue.ToFluidArrayAsync(await {forExpr}({ctx}), {ctx});");
            context.WriteLine("var length = list.Count;");

            if (usesForLoop)
            {
                context.WriteLine("var forloop = ForLoopValue.Create();");
                context.WriteLine("forloop.Length = length;");
                context.WriteLine($"{ctx}.LocalScope.SetOwnValue(\"forloop\", forloop);");
            }

            EmitAssignStatements();

            context.WriteLine("for (var i = 0; i < length; i++)");
            context.WriteLine("{");
            using (context.Indent())
            {
                context.WriteLine($"{ctx}.IncrementSteps();");
                context.WriteLine($"{ctx}.LocalScope.SetOwnValue({variableName}, list[i]);");

                if (usesForLoop)
                {
                    context.WriteLine("forloop.Index = i + 1;");
                    context.WriteLine("forloop.Index0 = i;");
                    context.WriteLine("forloop.RIndex = length - i;");
                    context.WriteLine("forloop.RIndex0 = length - i - 1;");
                    context.WriteLine("forloop.First = i == 0;");
                    context.WriteLine("forloop.Last = i == length - 1;");
                }

                context.WriteLine($"if ({call} == Completion.Break) break;");

                if (usesForLoop)
                {
                    context.WriteLine($"{ctx}.LocalScope.SetOwnValue(\"forloop\", forloop);");
                }
            }
            context.WriteLine("}");
            context.WriteLine("return Completion.Normal;");
        }
        else
        {
            // Keyword arguments alone, or nothing.
            EmitAssignStatements();
            context.WriteLine($"return {call};");
        }
    }
}
