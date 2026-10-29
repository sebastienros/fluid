using Fluid.Values;
using Fluid.SourceGeneration;

namespace Fluid.Ast;

public sealed class FilterExpression : Expression, ISynchronousSourceable
{
    public FilterExpression(Expression input, string name, IReadOnlyList<FilterArgument> parameters)
    {
        Input = input;
        Name = name;
        Parameters = parameters ?? [];
        _canBeCached = Parameters.All(x => x.Expression is LiteralExpression);
        _callSite = new FilterCallSite(name);
    }

    public Expression Input { get; }
    public string Name { get; }
    public IReadOnlyList<FilterArgument> Parameters { get; }

    // Whether the arguments can be cached: when all the parameters are literals. Decided once, as concurrent first
    // renders must not race on it (a render could cache arguments that are not literals for all the others).
    private readonly bool _canBeCached;
    private volatile FilterArguments _cachedArguments;

    // Resolves the filter by name and caches it, see FilterCallSite.
    private readonly FilterCallSite _callSite;

    public override ValueTask<FluidValue> EvaluateAsync(TemplateContext context)
    {
        var arguments = _cachedArguments;

        if (arguments is null)
        {
            // First evaluation, or arguments that can't be cached because they aren't all literals.
            return EvaluateWithArgumentsAsync(context);
        }

        ValueTask<FluidValue> inputTask;

        try
        {
            inputTask = Input.EvaluateAsync(context);
        }
        catch (Exception e)
        {
            // EvaluateAsync used to be async, so even a synchronous input failure faulted its task.
            return FilterCallSite.Faulted(e);
        }

        if (!inputTask.IsCompletedSuccessfully)
        {
            return AwaitedInput(inputTask, arguments, context);
        }

        // Nothing was awaited: invoke the filter directly and forward its ValueTask, so a synchronous
        // filter (the vast majority) doesn't allocate an async state machine.
        return _callSite.InvokeAsync(inputTask.Result, arguments, context);
    }

    private async ValueTask<FluidValue> EvaluateWithArgumentsAsync(TemplateContext context)
    {
        var arguments = new FilterArguments();

        foreach (var parameter in Parameters)
        {
            arguments.Add(parameter.Name, await parameter.Expression.EvaluateAsync(context));
        }

        // Can we cache it?
        if (_canBeCached)
        {
            _cachedArguments = arguments;
        }

        var input = await Input.EvaluateAsync(context);

        return await _callSite.InvokeAsync(input, arguments, context);
    }

    private async ValueTask<FluidValue> AwaitedInput(ValueTask<FluidValue> inputTask, FilterArguments arguments, TemplateContext context)
    {
        var input = await inputTask;
        return await _callSite.InvokeAsync(input, arguments, context);
    }

    protected internal override Expression Accept(AstVisitor visitor) => visitor.VisitFilterExpression(this);

    // Whether all the arguments are literals the generated code can build once.
    private bool HasLiteralArguments => _canBeCached && Parameters.All(p => SourceGenerationContext.IsSourceableLiteral(((LiteralExpression)p.Expression).Value));

    bool ISynchronousSourceable.IsSynchronous => HasLiteralArguments;

    public void WriteTo(SourceGenerationContext context)
    {
        var inputExpr = context.GetExpressionMethodName(Input);

        // Like the interpreter, the filter is resolved once per call site, and literal arguments are only built once.
        var callSite = context.GetOrAddStaticField("global::Fluid.Ast.FilterCallSite", "Filter_", $"new global::Fluid.Ast.FilterCallSite({SourceGenerationContext.ToCSharpStringLiteral(Name)})");

        if (TryGetLiteralArguments(context, out var literalArguments))
        {
            // Like the interpreter, the filter is invoked directly when the input completes synchronously.
            context.WriteLine($"var input = {inputExpr}({context.ContextName});");
            context.WriteLine("if (!input.IsCompletedSuccessfully)");
            context.WriteLine("{");
            using (context.Indent())
            {
                context.WriteLine($"return Awaited(input, {context.ContextName});");
            }
            context.WriteLine("}");
            context.WriteLine($"return {callSite}.InvokeAsync(input.Result, {literalArguments}, {context.ContextName});");
            context.WriteLine();
            context.WriteLine($"static async ValueTask<FluidValue> Awaited(ValueTask<FluidValue> input, TemplateContext {context.ContextName}) => await {callSite}.InvokeAsync(await input, {literalArguments}, {context.ContextName});");
            return;
        }

        context.WriteLine("var arguments = new FilterArguments();");
        for (var i = 0; i < Parameters.Count; i++)
        {
            var p = Parameters[i];
            var name = SourceGenerationContext.ToCSharpStringLiteral(p.Name);
            var expr = context.GetExpressionMethodName(p.Expression);
            context.WriteLine($"arguments.Add({name}, await {expr}({context.ContextName}));");
        }

        context.WriteLine($"var input = await {inputExpr}({context.ContextName});");
        context.WriteLine($"return await {callSite}.InvokeAsync(input, arguments, {context.ContextName});");
    }

    private bool TryGetLiteralArguments(SourceGenerationContext context, out string fieldName)
    {
        fieldName = null;

        if (!HasLiteralArguments)
        {
            return false;
        }

        var initializer = new System.Text.StringBuilder("new FilterArguments()");
        foreach (var parameter in Parameters)
        {
            context.TryGetLiteralValue(((LiteralExpression)parameter.Expression).Value, out var literal);

            initializer.Append(".Add(").Append(SourceGenerationContext.ToCSharpStringLiteral(parameter.Name)).Append(", ").Append(literal).Append(')');
        }

        fieldName = context.GetOrAddStaticField("FilterArguments", "Args_", initializer.ToString());
        return true;
    }
}
