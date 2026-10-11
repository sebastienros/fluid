using Fluid.Values;
using Fluid.SourceGeneration;

namespace Fluid.Ast.BinaryExpressions;

public sealed class StartsWithBinaryExpression : BinaryExpression, ISourceable
{
    public StartsWithBinaryExpression(Expression left, Expression right) : base(left, right)
    {
    }

    public override ValueTask<FluidValue> EvaluateAsync(TemplateContext context)
    {
        var leftTask = Left.EvaluateAsync(context);
        if (!leftTask.IsCompletedSuccessfully)
        {
            return Awaited(leftTask, default, context);
        }

        var rightTask = Right.EvaluateAsync(context);
        if (!rightTask.IsCompletedSuccessfully)
        {
            return Awaited(leftTask, rightTask, context);
        }

        var leftValue = leftTask.Result;
        var rightValue = rightTask.Result;

        if (leftValue.Type != FluidValues.Array)
        {
            return BinaryExpressionFluidValue.Create(leftValue, CompareStrings(leftValue, rightValue));
        }

        // Stay synchronous when the array answers synchronously, which the built-in ones do.
        var firstTask = leftValue.GetValueAsync("first", context);
        if (firstTask.IsCompletedSuccessfully)
        {
            return BinaryExpressionFluidValue.Create(leftValue, firstTask.Result.Equals(rightValue));
        }

        return AwaitedArray(firstTask, leftValue, rightValue);

        static async ValueTask<FluidValue> AwaitedArray(ValueTask<FluidValue> firstTask, FluidValue leftValue, FluidValue rightValue)
        {
            var first = await firstTask;
            return BinaryExpressionFluidValue.Create(leftValue, first.Equals(rightValue));
        }
    }

    private static bool CompareStrings(FluidValue leftValue, FluidValue rightValue)
    {
        return leftValue.ToStringValue().StartsWith(rightValue.ToStringValue(), StringComparison.Ordinal);
    }

    // The right operand is only started here when the left one had to be awaited first.
    private async ValueTask<FluidValue> Awaited(ValueTask<FluidValue> leftTask, ValueTask<FluidValue>? startedRightTask, TemplateContext context)
    {
        var leftValue = await leftTask;
        var rightValue = await (startedRightTask ?? Right.EvaluateAsync(context));

        bool comparisonResult;
        if (leftValue.Type == FluidValues.Array)
        {
            var first = await leftValue.GetValueAsync("first", context);
            comparisonResult = first.Equals(rightValue);
        }
        else
        {
            comparisonResult = CompareStrings(leftValue, rightValue);
        }

        return BinaryExpressionFluidValue.Create(leftValue, comparisonResult);
    }

    protected internal override Expression Accept(AstVisitor visitor) => visitor.VisitStartsWithBinaryExpression(this);

    public void WriteTo(SourceGenerationContext context)
    {
        var leftExpr = context.GetExpressionMethodName(Left);
        var rightExpr = context.GetExpressionMethodName(Right);

        context.WriteLine($"var leftValue = await {leftExpr}({context.ContextName});");
        context.WriteLine($"var rightValue = await {rightExpr}({context.ContextName});");

        context.WriteLine("bool comparisonResult;");
        context.WriteLine("if (leftValue.Type == FluidValues.Array)");
        context.WriteLine("{");
        using (context.Indent())
        {
            context.WriteLine($"var first = await leftValue.GetValueAsync(\"first\", {context.ContextName});");
            context.WriteLine("comparisonResult = first.Equals(rightValue);");
        }
        context.WriteLine("}");
        context.WriteLine("else");
        context.WriteLine("{");
        using (context.Indent())
        {
            context.WriteLine("comparisonResult = leftValue.ToStringValue().StartsWith(rightValue.ToStringValue(), StringComparison.Ordinal);");
        }
        context.WriteLine("}");

        context.WriteLine("return BinaryExpressionFluidValue.Create(leftValue, comparisonResult);");
    }
}
