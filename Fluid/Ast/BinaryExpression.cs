using System.Runtime.CompilerServices;
using Fluid.Values;

namespace Fluid.Ast;

public abstract class BinaryExpression : Expression
{
    protected BinaryExpression(Expression left, Expression right)
    {
        Left = left;
        Right = right;
    }

    public Expression Left { get; }

    /// <summary>
    /// Whether the expression is the <c>blank</c> or <c>empty</c> keyword, which conditions handle specially.
    /// </summary>
    private protected static bool IsBlankOrEmptyKeyword(Expression expression)
    {
        return expression is LiteralExpression { Value: BlankValue or EmptyValue };
    }

    public Expression Right { get; }

    /// <summary>
    /// Evaluates two operands and tries to avoid state machines.
    /// </summary>
    public override ValueTask<FluidValue> EvaluateAsync(TemplateContext context)
    {
        var leftTask = Left.EvaluateAsync(context);
        var rightTask = Right.EvaluateAsync(context);

        if (leftTask.IsCompletedSuccessfully && rightTask.IsCompletedSuccessfully)
        {
            return Evaluate(leftTask.Result, rightTask.Result, context);
        }

        return Awaited(leftTask, rightTask, context);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private async ValueTask<FluidValue> Awaited(
        ValueTask<FluidValue> leftTask,
        ValueTask<FluidValue> rightTask,
        TemplateContext context)
    {
        var leftValue = await leftTask;
        var rightValue = await rightTask;

        return Evaluate(leftValue, rightValue, context);
    }

    /// <summary>
    /// Evaluates the left operand, and the right one only when the left one doesn't decide the result,
    /// like Liquid's <c>and</c> and <c>or</c>.
    /// </summary>
    /// <param name="context">The template context.</param>
    /// <param name="decidingValue">The truthiness of the left operand that decides the result.</param>
    private protected ValueTask<FluidValue> EvaluateShortCircuitAsync(TemplateContext context, bool decidingValue)
    {
        var leftTask = Left.EvaluateAsync(context);

        if (!leftTask.IsCompletedSuccessfully)
        {
            return AwaitedLeft(leftTask, context, decidingValue);
        }

        return EvaluateRight(leftTask.Result, context, decidingValue);
    }

    private ValueTask<FluidValue> EvaluateRight(FluidValue leftValue, TemplateContext context, bool decidingValue)
    {
        if (leftValue.ToBooleanValue(context) == decidingValue)
        {
            return BinaryExpressionFluidValue.Create(leftValue, decidingValue);
        }

        var rightTask = Right.EvaluateAsync(context);

        if (rightTask.IsCompletedSuccessfully)
        {
            return Evaluate(leftValue, rightTask.Result, context);
        }

        return AwaitedRight(leftValue, rightTask, context);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private async ValueTask<FluidValue> AwaitedLeft(ValueTask<FluidValue> leftTask, TemplateContext context, bool decidingValue)
    {
        return await EvaluateRight(await leftTask, context, decidingValue);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private async ValueTask<FluidValue> AwaitedRight(FluidValue leftValue, ValueTask<FluidValue> rightTask, TemplateContext context)
    {
        return Evaluate(leftValue, await rightTask, context);
    }

    internal virtual FluidValue Evaluate(FluidValue leftValue, FluidValue rightValue, TemplateContext context)
        => Evaluate(leftValue, rightValue);

    // sub-classes using the default implementation need to override this
    internal virtual FluidValue Evaluate(FluidValue leftValue, FluidValue rightValue)
    {
        throw new NotImplementedException();
    }
}
