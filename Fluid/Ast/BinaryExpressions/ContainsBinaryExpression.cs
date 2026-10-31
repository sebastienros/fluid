using Fluid.Values;
using Fluid.SourceGeneration;

namespace Fluid.Ast.BinaryExpressions;

public sealed class ContainsBinaryExpression : BinaryExpression, ISourceable
{
    // Like Liquid, blank and empty are compared as their text, '', which any string contains, and
    // which nothing else does. The literals themselves contain nothing, not even each other.
    private readonly bool _containsKeyword;
    private readonly bool _keywordContainsKeyword;

    public ContainsBinaryExpression(Expression left, Expression right) : base(left, right)
    {
        _containsKeyword = IsBlankOrEmptyKeyword(right);
        _keywordContainsKeyword = _containsKeyword && IsBlankOrEmptyKeyword(left);
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

        if (_containsKeyword)
        {
            return BinaryExpressionFluidValue.Create(leftValue, !_keywordContainsKeyword && ContainsKeyword(leftValue));
        }

        if (IsNilOrFalse(leftValue, context) || IsNilOrFalse(rightValue, context))
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

    // A string, or a value assigned from blank or empty, which is '' in Liquid.
    private static bool ContainsKeyword(FluidValue value) => value.Type is FluidValues.String or FluidValues.Blank or FluidValues.Empty;

    // Shopify Liquid behavior: `contains` returns false if either operand is nil/false.
    // (see Liquid::Condition operators['contains'] guard: `if left && right && left.respond_to?(:include?)`).
    private static bool IsNilOrFalse(FluidValue value, TemplateContext context)
    {
        return value.IsNil() || (value.Type == FluidValues.Boolean && !value.ToBooleanValue(context));
    }

    // The right operand is only started here when the left one had to be awaited first.
    private async ValueTask<FluidValue> Awaited(ValueTask<FluidValue> leftTask, ValueTask<FluidValue>? startedRightTask, TemplateContext context)
    {
        var leftValue = await leftTask;
        var rightValue = await (startedRightTask ?? Right.EvaluateAsync(context));

        if (_containsKeyword)
        {
            return BinaryExpressionFluidValue.Create(leftValue, !_keywordContainsKeyword && ContainsKeyword(leftValue));
        }

        if (IsNilOrFalse(leftValue, context) || IsNilOrFalse(rightValue, context))
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

        if (_containsKeyword)
        {
            context.WriteLine(_keywordContainsKeyword
                ? "return BinaryExpressionFluidValue.Create(leftValue, false);"
                : "return BinaryExpressionFluidValue.Create(leftValue, leftValue.Type is FluidValues.String or FluidValues.Blank or FluidValues.Empty);");
            return;
        }

        context.WriteLine("if (leftValue.IsNil() || (leftValue.Type == FluidValues.Boolean && !leftValue.ToBooleanValue(context))");
        context.WriteLine("    || rightValue.IsNil() || (rightValue.Type == FluidValues.Boolean && !rightValue.ToBooleanValue(context)))");
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
