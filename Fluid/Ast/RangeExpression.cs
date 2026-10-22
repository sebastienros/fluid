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

        context.WriteLine($"var start = Convert.ToInt32((await {fromMethod}({context.ContextName})).ToNumberValue());");
        context.WriteLine($"var end = Convert.ToInt32((await {toMethod}({context.ContextName})).ToNumberValue());");
        context.WriteLine("// If end < start, we create an empty array");
        context.WriteLine("var length = end < start ? 0L : (long)end - start + 1;");
        context.WriteLine($"{context.ContextName}.EnsureCollectionSize(length);");
        context.WriteLine("var list = new FluidValue[checked((int)length)];");
        context.WriteLine("for (var i = 0; i < list.Length; i++)");
        context.WriteLine("{");
        using (context.Indent())
        {
            context.WriteLine($"{context.ContextName}.IncrementSteps();");
            context.WriteLine("list[i] = NumberValue.Create(start + i);");
        }
        context.WriteLine("}");
        context.WriteLine("return ArrayValue.Create(list);");
    }
}
