using System.ComponentModel;
using Fluid.Utils;
using Fluid.Values;
using System.Runtime.CompilerServices;
using Fluid.SourceGeneration;

namespace Fluid.Ast;

public sealed class RangeExpression : Expression, ISourceable
{
    public RangeExpression(Expression from, Expression to)
    {
        From = from;
        To = to;
    }

    public Expression From { get; }

    public Expression To { get; }

    public override ValueTask<FluidValue> EvaluateAsync(TemplateContext context)
    {
        int start, end;

        var startTask = From.EvaluateAsync(context);
        var endTask = To.EvaluateAsync(context);

        if (startTask.IsCompletedSuccessfully && endTask.IsCompletedSuccessfully)
        {
            start = ToBound(startTask.Result);
            end = ToBound(endTask.Result);

            return BuildArray(start, end, context);
        }
        else
        {
            return Awaited(startTask, endTask, context);
        }
    }

    /// <summary>
    /// Converts a bound of a range, which has to fit in an int since the range is materialized.
    /// </summary>
    internal static int ToBound(FluidValue value)
    {
        var number = value.ToNumberValue();

        if (number >= int.MaxValue + 0.5m || number <= int.MinValue - 0.5m)
        {
            throw new LiquidException($"The range bound {value.ToStringValue()} is out of range.");
        }

        return Convert.ToInt32(number);
    }

    /// <summary>
    /// Converts a range bound. Used by source-generated templates, which can only call public members.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static int GetBound(FluidValue value) => ToBound(value);

    /// <summary>
    /// Creates the items of a range. Used by source-generated templates, which can only call public members.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static ArrayValue CreateRange(int start, int end, TemplateContext context) => BuildArray(start, end, context);

    internal static ArrayValue BuildArray(int start, int end, TemplateContext context)
    {
        // If end < start, we create an empty array
        var length = end < start ? 0L : (long)end - start + 1;
        context.EnsureCollectionSize(length);

        if (length > int.MaxValue)
        {
            throw new LiquidException($"The range ({start}..{end}) has too many items.");
        }

        var list = new FluidValue[length];

        for (var i = 0; i < list.Length; i++)
        {
            context.IncrementSteps();
            list[i] = NumberValue.Create(start + i);
        }

        return ArrayValue.Create(list);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async ValueTask<FluidValue> Awaited(
        ValueTask<FluidValue> leftTask,
        ValueTask<FluidValue> rightTask,
        TemplateContext context)
    {
        var start = ToBound(await leftTask);
        var end = ToBound(await rightTask);

        return BuildArray(start, end, context);
    }

    protected internal override Expression Accept(AstVisitor visitor) => visitor.VisitRangeExpression(this);

    public void WriteTo(SourceGenerationContext context)
    {
        var fromMethod = context.GetExpressionMethodName(From);
        var toMethod = context.GetExpressionMethodName(To);

        // The same checks as the interpreter: bounds out of the int range and ranges with too many
        // items throw a LiquidException.
        context.WriteLine($"var start = global::Fluid.Ast.RangeExpression.GetBound(await {fromMethod}({context.ContextName}));");
        context.WriteLine($"var end = global::Fluid.Ast.RangeExpression.GetBound(await {toMethod}({context.ContextName}));");
        context.WriteLine($"return global::Fluid.Ast.RangeExpression.CreateRange(start, end, {context.ContextName});");
    }
}
