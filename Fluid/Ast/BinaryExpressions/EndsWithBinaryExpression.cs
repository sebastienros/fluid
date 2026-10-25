using Fluid.Values;
using Fluid.SourceGeneration;

namespace Fluid.Ast.BinaryExpressions;

public sealed class EndsWithBinaryExpression : BinaryExpression, ISourceable
{
    public EndsWithBinaryExpression(Expression left, Expression right) : base(left, right)
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
            return BinaryExpressionFluidValue.Create(leftValue, CompareStrings(leftValue, rightValue, context));
        }

        // Stay synchronous when the array answers synchronously, which the built-in ones do.
        var lastTask = leftValue.GetValueAsync("last", context);
        if (lastTask.IsCompletedSuccessfully)
        {
            return BinaryExpressionFluidValue.Create(leftValue, lastTask.Result.Equals(rightValue));
        }

        return AwaitedArray(lastTask, leftValue, rightValue);

        static async ValueTask<FluidValue> AwaitedArray(ValueTask<FluidValue> lastTask, FluidValue leftValue, FluidValue rightValue)
        {
            var last = await lastTask;
            return BinaryExpressionFluidValue.Create(leftValue, last.Equals(rightValue));
        }
    }

    private static bool CompareStrings(FluidValue leftValue, FluidValue rightValue, TemplateContext context)
    {
        return leftValue.ToStringValue(context).EndsWith(rightValue.ToStringValue(context), StringComparison.Ordinal);
    }

    // The right operand is only started here when the left one had to be awaited first.
    private async ValueTask<FluidValue> Awaited(ValueTask<FluidValue> leftTask, ValueTask<FluidValue>? startedRightTask, TemplateContext context)
    {
        var leftValue = await leftTask;
        var rightValue = await (startedRightTask ?? Right.EvaluateAsync(context));

        bool comparisonResult;
        if (leftValue.Type == FluidValues.Array)
        {
            var last = await leftValue.GetValueAsync("last", context);
            comparisonResult = last.Equals(rightValue);
        }
        else
        {
            comparisonResult = CompareStrings(leftValue, rightValue, context);
        }

        return BinaryExpressionFluidValue.Create(leftValue, comparisonResult);
    }

    protected internal override Expression Accept(AstVisitor visitor) => visitor.VisitEndsWithBinaryExpression(this);

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
            context.WriteLine($"var last = await leftValue.GetValueAsync(\"last\", {context.ContextName});");
            context.WriteLine("comparisonResult = last.Equals(rightValue);");
        }
        context.WriteLine("}");
        context.WriteLine("else");
        context.WriteLine("{");
        using (context.Indent())
        {
            context.WriteLine("comparisonResult = leftValue.ToStringValue(context).EndsWith(rightValue.ToStringValue(context), StringComparison.Ordinal);");
        }
        context.WriteLine("}");

        context.WriteLine("return BinaryExpressionFluidValue.Create(leftValue, comparisonResult);");
    }
}
