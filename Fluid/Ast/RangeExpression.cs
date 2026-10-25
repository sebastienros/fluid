using System.Globalization;
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
            start = ToBound(startTask.Result, From is LiteralExpression, context);
            end = ToBound(endTask.Result, To is LiteralExpression, context);

            return CreateRangeValue(start, end, context);
        }
        else
        {
            return Awaited(startTask, endTask, this, context);
        }
    }

    /// <summary>
    /// Converts a bound of a range like Liquid, to an int.
    /// Literal numbers are truncated. Nil and strings are converted like Ruby's <c>to_i</c>, and other values
    /// must be integers (a float from a variable is an error).
    /// </summary>
    internal static int ToBound(FluidValue value, bool isLiteral, TemplateContext context)
    {
        switch (value.Type)
        {
            case FluidValues.Nil:
            case FluidValues.Empty:
            case FluidValues.Blank:
                return 0;

            case FluidValues.String:
                return ParseLeadingInteger(value.ToStringValue(context), value, context);

            case FluidValues.Number:
                var number = value.ToNumberValue(context);

                if (!isLiteral && NumberValue.GetScale(number) != 0)
                {
                    throw new LiquidException($"Invalid integer '{value.ToStringValue(context)}' in range expression.");
                }

                return CheckBound(Math.Truncate(number), value, context);

            default:
                // Like Ruby's Integer(value.to_s).
                var text = value.ToStringValue(context);

                if (!decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite, CultureInfo.InvariantCulture, out var integer))
                {
                    throw new LiquidException($"Invalid integer '{text}' in range expression.");
                }

                return CheckBound(integer, value, context);
        }
    }

    // Like Ruby's String#to_i: optional leading whitespace and sign, then digits, and 0 if there are none.
    private static int ParseLeadingInteger(string text, FluidValue value, TemplateContext context)
    {
        var i = 0;

        while (i < text.Length && (char.IsWhiteSpace(text[i])))
        {
            i++;
        }

        var negative = false;

        if (i < text.Length && (text[i] == '-' || text[i] == '+'))
        {
            negative = text[i] == '-';
            i++;
        }

        decimal result = 0;

        for (; i < text.Length; i++)
        {
            var c = text[i];

            if (c >= '0' && c <= '9')
            {
                result = result * 10 + (c - '0');

                if (result > int.MaxValue + 1m)
                {
                    break;
                }
            }
            else if (c != '_' || i + 1 >= text.Length || text[i + 1] < '0' || text[i + 1] > '9' || text[i - 1] < '0' || text[i - 1] > '9')
            {
                break;
            }
        }

        return CheckBound(negative ? -result : result, value, context);
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
    public static int GetBound(FluidValue value, bool isLiteral, TemplateContext context) => ToBound(value, isLiteral, context);

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
        RangeExpression range,
        TemplateContext context)
    {
        var start = ToBound(await leftTask, range.From is LiteralExpression, context);
        var end = ToBound(await rightTask, range.To is LiteralExpression, context);

        return CreateRangeValue(start, end, context);
    }

    protected internal override Expression Accept(AstVisitor visitor) => visitor.VisitRangeExpression(this);

    public void WriteTo(SourceGenerationContext context)
    {
        var fromMethod = context.GetExpressionMethodName(From);
        var toMethod = context.GetExpressionMethodName(To);

        // The same checks as the interpreter: bounds out of the int range and ranges with too many
        // items throw a LiquidException.
        context.WriteLine($"var start = global::Fluid.Ast.RangeExpression.GetBound(await {fromMethod}({context.ContextName}), {(From is LiteralExpression ? "true" : "false")}, {context.ContextName});");
        context.WriteLine($"var end = global::Fluid.Ast.RangeExpression.GetBound(await {toMethod}({context.ContextName}), {(To is LiteralExpression ? "true" : "false")}, {context.ContextName});");
        context.WriteLine($"return global::Fluid.Ast.RangeExpression.CreateRange(start, end, {context.ContextName});");
    }
}
