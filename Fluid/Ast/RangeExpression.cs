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
            start = ToBound(startTask.Result, context);
            end = ToBound(endTask.Result, context);

            return CreateRangeValue(start, end, context);
        }
        else
        {
            return Awaited(startTask, endTask, context);
        }
    }

    /// <summary>
    /// Converts a bound of a range to an int, like Liquid: nil is 0, strings are converted to numbers and truncated,
    /// and any other value must be an integer (a float from a variable is an error).
    /// Literal bounds are already truncated by the parser.
    /// </summary>
    internal static int ToBound(FluidValue value, TemplateContext context)
    {
        switch (value.Type)
        {
            case FluidValues.Nil:
            case FluidValues.Empty:
            case FluidValues.Blank:
                return 0;

            case FluidValues.String:
                return CheckBound(decimal.Truncate(value.ToNumberValue(context)), value, context);

            case FluidValues.Number when value.IsInteger(context):
                return CheckBound(value.ToNumberValue(context), value, context);

            default:
                throw new LiquidException($"Invalid integer '{value.ToStringValue(context)}' in range expression.");
        }
    }

    /// <summary>
    /// Checks that a bound fits in an int, since the items of a range are counted with an int.
    /// </summary>
    private static int CheckBound(decimal number, FluidValue value, TemplateContext context)
    {
        if (number > int.MaxValue || number < int.MinValue)
        {
            throw new LiquidException($"The range bound {value.ToStringValue(context)} is out of range.");
        }

        return (int)number;
    }

    /// <summary>
    /// Converts a range bound. Used by source-generated templates, which can only call public members.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static int GetBound(FluidValue value, TemplateContext context) => ToBound(value, context);

    /// <summary>
    /// Creates a range. Used by source-generated templates, which can only call public members.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static RangeValue CreateRange(int start, int end, TemplateContext context) => CreateRangeValue(start, end, context);

    internal static RangeValue CreateRangeValue(int start, int end, TemplateContext context)
    {
        // If end < start, the range is empty
        var length = end < start ? 0L : (long)end - start + 1;
        context.EnsureCollectionSize(length);

        if (length > int.MaxValue)
        {
            throw new LiquidException($"The range ({start}..{end}) has too many items.");
        }

        return RangeValue.Create(start, end);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async ValueTask<FluidValue> Awaited(
        ValueTask<FluidValue> leftTask,
        ValueTask<FluidValue> rightTask,
        TemplateContext context)
    {
        var start = ToBound(await leftTask, context);
        var end = ToBound(await rightTask, context);

        return CreateRangeValue(start, end, context);
    }

    protected internal override Expression Accept(AstVisitor visitor) => visitor.VisitRangeExpression(this);

    public void WriteTo(SourceGenerationContext context)
    {
        var fromMethod = context.GetExpressionMethodName(From);
        var toMethod = context.GetExpressionMethodName(To);

        // The same checks as the interpreter: bounds out of the int range and ranges with too many
        // items throw a LiquidException.
        context.WriteLine($"var start = global::Fluid.Ast.RangeExpression.GetBound(await {fromMethod}({context.ContextName}), {context.ContextName});");
        context.WriteLine($"var end = global::Fluid.Ast.RangeExpression.GetBound(await {toMethod}({context.ContextName}), {context.ContextName});");
        context.WriteLine($"return global::Fluid.Ast.RangeExpression.CreateRange(start, end, {context.ContextName});");
    }
}
