using Fluid.Values;

namespace Fluid.Ast;

public sealed class IndexerSegment : MemberSegment
{
    public IndexerSegment(Expression expression)
    {
        Expression = expression;
    }

    public Expression Expression { get; }

    public override ValueTask<FluidValue> ResolveAsync(FluidValue value, TemplateContext context)
    {
        var indexTask = Expression.EvaluateAsync(context);

        // Stay synchronous when the index is, which it is for a literal or a variable.
        if (indexTask.IsCompletedSuccessfully)
        {
            return value.GetIndexAsync(indexTask.Result, context);
        }

        return Awaited(indexTask, value, context);

        static async ValueTask<FluidValue> Awaited(ValueTask<FluidValue> indexTask, FluidValue value, TemplateContext context)
        {
            var index = await indexTask;
            return await value.GetIndexAsync(index, context);
        }
    }

    public override async ValueTask<(FluidValue Value, bool UseModelFallback)> ResolveFromScopeAsync(TemplateContext context)
    {
        // Evaluate the expression to get the key
        var key = await Expression.EvaluateAsync(context);
        var keyString = key.ToStringValue();

        // Look up the variable in scope
        var value = context.LocalScope.GetValue(keyString);

        if (value.IsNil())
        {
            // Check if there's an increment/decrement counter with this name
            var incDecValue = context.LocalScope.GetValue(IncrementStatement.Prefix + keyString);
            if (!incDecValue.IsNil())
            {
                return (incDecValue, false);
            }

            // Try to access the model with the key
            if (!context.Model.IsNil())
            {
                value = await context.Model.GetValueAsync(keyString, context);
            }
        }

        // IndexerSegment doesn't use model fallback the same way IdentifierSegment does
        return (value, false);
    }

    public override string GetSegmentName()
    {
        // For indexer segments, return a representation like [index]
        // Since we don't have the evaluated value here, we return a generic representation
        return "[index]";
    }
}
