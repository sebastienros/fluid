using Fluid.Values;
using Fluid.SourceGeneration;

namespace Fluid.Ast.BinaryExpressions;

public sealed class ContainsBinaryExpression : BinaryExpression, ISourceable
{
    public ContainsBinaryExpression(Expression left, Expression right) : base(left, right)
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

        if (IsNilOrFalse(leftValue) || IsNilOrFalse(rightValue))
        {
            return BinaryExpressionFluidValue.Create(leftValue, false);
        }

        // Stay synchronous when the value answers synchronously, which the built-in ones do.
        var containsTask = leftValue.ContainsAsync(rightValue, context);
        if (containsTask.IsCompletedSuccessfully)
        {
            return BinaryExpressionFluidValue.Create(leftValue, containsTask.Result);
        }

        return AwaitedContains(containsTask, leftValue);

        static async ValueTask<FluidValue> AwaitedContains(ValueTask<bool> containsTask, FluidValue leftValue)
        {
            return BinaryExpressionFluidValue.Create(leftValue, await containsTask);
        }
    }

    // Shopify Liquid behavior: `contains` returns false if either operand is nil/false.
    // (see Liquid::Condition operators['contains'] guard: `if left && right && left.respond_to?(:include?)`).
    private static bool IsNilOrFalse(FluidValue value)
    {
        return value.IsNil() || (value.Type == FluidValues.Boolean && !value.ToBooleanValue());
    }

    // The right operand is only started here when the left one had to be awaited first.
    private async ValueTask<FluidValue> Awaited(ValueTask<FluidValue> leftTask, ValueTask<FluidValue>? startedRightTask, TemplateContext context)
    {
        var leftValue = await leftTask;
        var rightValue = await (startedRightTask ?? Right.EvaluateAsync(context));

        if (IsNilOrFalse(leftValue) || IsNilOrFalse(rightValue))
        {
            return BinaryExpressionFluidValue.Create(leftValue, false);
        }

        var comparisonResult = await leftValue.ContainsAsync(rightValue, context);
        return BinaryExpressionFluidValue.Create(leftValue, comparisonResult);
    }

    protected internal override Expression Accept(AstVisitor visitor) => visitor.VisitContainsBinaryExpression(this);

    public void WriteTo(SourceGenerationContext context)
    {
        var leftExpr = context.GetExpressionMethodName(Left);
        var rightExpr = context.GetExpressionMethodName(Right);

        context.WriteLine($"var leftValue = await {leftExpr}({context.ContextName});");
        context.WriteLine($"var rightValue = await {rightExpr}({context.ContextName});");
        context.WriteLine("if (leftValue.IsNil() || (leftValue.Type == FluidValues.Boolean && !leftValue.ToBooleanValue())");
        context.WriteLine("    || rightValue.IsNil() || (rightValue.Type == FluidValues.Boolean && !rightValue.ToBooleanValue()))");
        context.WriteLine("{");
        using (context.Indent())
        {
            context.WriteLine("return BinaryExpressionFluidValue.Create(leftValue, false);");
        }
        context.WriteLine("}");
        context.WriteLine($"var comparisonResult = await leftValue.ContainsAsync(rightValue, {context.ContextName});");
        context.WriteLine("return BinaryExpressionFluidValue.Create(leftValue, comparisonResult);");
    }
}
