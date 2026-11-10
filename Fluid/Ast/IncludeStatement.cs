using Fluid.SourceGeneration;
using Fluid.Values;
using System.ComponentModel;
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

        var templateName = (await Path.EvaluateAsync(context)).ToStringValue(context);
        var loadedTemplate = await TemplateLoader.LoadAsync(
            Parser,
            templateName,
            context,
            context.Options.DefaultFileExtension);
        var template = loadedTemplate.Template;

        var identifier = System.IO.Path.GetFileNameWithoutExtension(loadedTemplate.Path);

        // Unlike render, include shares scope with the parent template.
        // Use a for-loop scope which passes through variable assignments to the parent.
        // This allows variables assigned inside the include to persist in the outer scope.
        using var scope = context.EnterScope(ScopeBehavior.WriteThrough);

        // Like Liquid, the value is evaluated before the keyword arguments. Without with or for, it is the variable
        // named like the partial: {% include 'product' %} renders the partial for each item of an array named product.
        var value = With ?? For;
        var evaluatedValue = value != null
            ? await value.EvaluateAsync(context)
            : await FindVariableAsync(templateName, context);

        // Keyword arguments are local to the include
        for (var i = 0; i < AssignStatements.Count; i++)
        {
            var stmt = AssignStatements[i];
            context.LocalScope.SetOwnValue(stmt.Identifier, await stmt.Value.EvaluateAsync(context));
        }

        // The bound variable is local to this include, and like Liquid it replaces a keyword argument of the same name,
        // even when it is nil.
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

            // Like Liquid, a break or continue doesn't stop the include, which renders every item, but stays on the
            // context: the next items render up to their first tag or output, and each break or continue they reach
            // is stacked, so that the ones the enclosing loop doesn't consume apply to the loops around it.
            if (result != Completion.Normal)
            {
                context.PushInterrupt(result);
            }

            context.LocalScope.SetOwnValue(variableName, list[i]);

            result = await RenderStatementsAsync(template, output, encoder, context);
        }

        return result;
    }

    /// <summary>
    /// Finds the variable an <c>include</c> without <c>with</c> or <c>for</c> renders the partial with. Like Liquid, it is
    /// named like the template as written: <c>product</c> for <c>'product'</c>, or <c>'product.liquid'</c> with the default
    /// extension, and <c>snippets/product</c>, which is never a variable, for <c>'snippets/product'</c>.
    /// </summary>
    /// <returns>The value of the variable, or nil when it doesn't exist, which like Liquid isn't an undefined variable,
    /// even with <see cref="TemplateOptions.StrictVariables"/>.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static ValueTask<FluidValue> FindVariableAsync(string templateName, TemplateContext context)
    {
        var extension = context.Options.DefaultFileExtension;
        var name = !string.IsNullOrEmpty(extension) && templateName.EndsWith(extension, StringComparison.OrdinalIgnoreCase)
            ? templateName.Substring(0, templateName.Length - extension.Length)
            : templateName;

        var value = context.LocalScope.GetValue(name);

        if (!value.IsNil() || context.Model.IsNil())
        {
            return new ValueTask<FluidValue>(value.IsNil() ? NilValue.Instance : value);
        }

        return FromModelAsync(context.Model.GetValueAsync(name, context));

        static async ValueTask<FluidValue> FromModelAsync(ValueTask<FluidValue> task)
        {
            var value = await task;
            return value.IsNil() ? NilValue.Instance : value;
        }
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
            if (context.HasPendingInterrupts)
            {
                // Like Liquid, the partial renders up to its first tag or output. Rare enough not to cache the statement.
                return await new PendingInterruptsStatement(statementList.Statements, skipText: false).WriteToAsync(output, encoder, context);
            }

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

        // Like the interpreter: the value is evaluated before the keyword arguments, and only an array is iterated.
        // Without with or for, it is the variable named like the partial.
        var valueExpr = value == null
            ? $"global::Fluid.Ast.IncludeStatement.FindVariableAsync({SourceGenerationContext.ToCSharpStringLiteral(path)}, {ctx})"
            : $"{context.GetExpressionMethodName(value)}({ctx})";
        context.WriteLine($"var includeValue = await {valueExpr};");
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

            // Like the interpreter, a break or continue stays pending while the next items render.
            context.WriteLine($"if (result != Completion.Normal) {ctx}.PushInterrupt(result);");
            context.WriteLine($"{ctx}.LocalScope.SetOwnValue({variableName}, list[i]);");
            context.WriteLine($"result = {call};");
        }
        context.WriteLine("}");
        context.WriteLine("return result;");
    }
}
