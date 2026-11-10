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

        if (context.IsIncludeDisabled)
        {
            ThrowIncludeNotAllowed();
        }

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

        // Like Liquid, the value is evaluated before the keyword arguments.
        var value = With ?? For;
        var evaluatedValue = value != null ? await value.EvaluateAsync(context) : null;

        // Keyword arguments are local to the include
        for (var i = 0; i < AssignStatements.Count; i++)
        {
            var stmt = AssignStatements[i];
            context.LocalScope.SetOwnValue(stmt.Identifier, await stmt.Value.EvaluateAsync(context));
        }

        if (evaluatedValue == null)
        {
            // Keyword arguments alone, or nothing, e.g. {% include 'products' %}.
            return await RenderStatementsAsync(template, output, encoder, context);
        }

        // The bound variable is local to this include, and like Liquid it replaces a keyword argument of the same name.
        var variableName = Alias ?? identifier;

        if (!IsIterated(evaluatedValue))
        {
            context.LocalScope.SetOwnValue(variableName, evaluatedValue);
            return await RenderStatementsAsync(template, output, encoder, context);
        }

        var list = await ArrayValue.ToFluidArrayAsync(evaluatedValue, context);
        var length = list.Count;
        var result = Completion.Normal;

        for (var i = 0; i < length; i++)
        {
            context.IncrementSteps();

            context.LocalScope.SetOwnValue(variableName, list[i]);

            // Like Liquid, a break or continue doesn't stop the include, which renders every item, but
            // applies to the enclosing loop.
            var completion = await RenderStatementsAsync(template, output, encoder, context);

            if (completion != Completion.Normal)
            {
                result = completion;
            }
        }

        return result;
    }

    /// <summary>
    /// Whether <c>include</c> renders the partial once for each item of a value: like Liquid, with both
    /// <c>with</c> and <c>for</c>, only an array is, and any other value (a range, a hash, a string, nil)
    /// is rendered once.
    /// </summary>
    internal static bool IsIterated(FluidValue value) => value.Type == FluidValues.Array && value is not RangeValue;

    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    private static void ThrowIncludeNotAllowed()
    {
        // Like Liquid, a partial rendered with render can't use include.
        throw new LiquidException(Fluid.Parser.ErrorMessages.IncludeNotAllowed);
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

        // Like the interpreter, a partial rendered with render can't use include.
        context.WriteLine($"if ({ctx}.IsIncludeDisabled) throw new LiquidException({SourceGenerationContext.ToCSharpStringLiteral(Fluid.Parser.ErrorMessages.IncludeNotAllowed)});");
        context.WriteLine($"using var scope = {ctx}.EnterScope(ScopeBehavior.WriteThrough);");

        var value = With ?? For;

        if (value == null)
        {
            // Keyword arguments alone, or nothing.
            EmitAssignStatements();
            context.WriteLine($"return {call};");
            return;
        }

        // Like the interpreter: the value is evaluated before the keyword arguments, and only an array is iterated.
        var valueExpr = context.GetExpressionMethodName(value);
        context.WriteLine($"var includeValue = await {valueExpr}({ctx});");
        EmitAssignStatements();

        context.WriteLine("if (includeValue.Type != FluidValues.Array || includeValue is RangeValue)");
        context.WriteLine("{");
        using (context.Indent())
        {
            context.WriteLine($"{ctx}.LocalScope.SetOwnValue({variableName}, includeValue);");
            context.WriteLine($"return {call};");
        }
        context.WriteLine("}");

        context.WriteLine($"var list = await ArrayValue.ToFluidArrayAsync(includeValue, {ctx});");
        context.WriteLine("var length = list.Count;");
        context.WriteLine("var result = Completion.Normal;");
        context.WriteLine("for (var i = 0; i < length; i++)");
        context.WriteLine("{");
        using (context.Indent())
        {
            context.WriteLine($"{ctx}.IncrementSteps();");
            context.WriteLine($"{ctx}.LocalScope.SetOwnValue({variableName}, list[i]);");
            context.WriteLine($"var completion = {call};");
            context.WriteLine("if (completion != Completion.Normal) result = completion;");
        }
        context.WriteLine("}");
        context.WriteLine("return result;");
    }
}
