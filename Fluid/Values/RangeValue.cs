using System.Globalization;
using System.Text.Encodings.Web;

namespace Fluid.Values;

/// <summary>
/// A range of integers, like <c>(1..5)</c>. Like a Ruby range, it is iterated as an array of numbers,
/// but renders and converts to a string as <c>start..end</c>, and it can't be read by index.
/// </summary>
public sealed class RangeValue : FluidValue, IFluidArray
{
    /// <summary>
    /// Creates the range of the integers from <paramref name="start"/> to <paramref name="end"/>, both included.
    /// </summary>
    public static RangeValue Create(long start, long end) => new RangeValue(start, end);

    private RangeValue(long start, long end)
    {
        Start = start;
        End = end;

        // The distance between the bounds always fits in an unsigned long.
        Count = end < start ? 0 : checked((int)(unchecked((ulong)(end - start)) + 1));
    }

    /// <summary>
    /// Gets the first number of the range.
    /// </summary>
    public long Start { get; }

    /// <summary>
    /// Gets the last number of the range, which is lower than <see cref="Start"/> when the range is empty.
    /// </summary>
    public long End { get; }

    /// <summary>
    /// Gets the number of items of the range.
    /// </summary>
    public int Count { get; }

    public FluidValue this[int index] => NumberValue.Create(Start + index);

    public override FluidValues Type => FluidValues.Array;

    public override bool Equals(FluidValue other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        // Like Ruby's Range#==, a range is only equal to a range with the same bounds: not to an array with
        // the same items, and not to another empty range. Like Liquid, where a range doesn't respond to empty?,
        // a range is never equal to empty, blank or nil either.
        return other is RangeValue range && Start == range.Start && End == range.End;
    }

    public override ValueTask<FluidValue> GetValueAsync(string name, TemplateContext context)
    {
        // Like Ruby's Range#first and Range#last, the bounds are returned even if the range is empty.
        return name switch
        {
            "size" => NumberValue.Create(Count),
            "first" => NumberValue.Create(Start),
            "last" => NumberValue.Create(End),
            _ => NilValue.Instance,
        };
    }

    public override bool ToBooleanValue(TemplateContext context)
    {
        return true;
    }

    public override decimal ToNumberValue(TemplateContext context)
    {
        // Like Liquid, a value that isn't a number is 0.
        return 0;
    }

    public override string ToStringValue(TemplateContext context)
    {
        return Start.ToString(CultureInfo.InvariantCulture) + ".." + End.ToString(CultureInfo.InvariantCulture);
    }

    public override ValueTask WriteToAsync(IFluidOutput output, TextEncoder encoder, TemplateContext context)
    {
        AssertWriteToParameters(output, encoder, context);

        output.Write(encoder, ToStringValue(context));
        return default;
    }

    public override object ToObjectValue(TemplateContext context)
    {
        var values = new object[Count];

        for (var i = 0; i < values.Length; i++)
        {
            values[i] = (decimal)(Start + i);
        }

        return values;
    }

    public override ValueTask<bool> ContainsAsync(FluidValue value, TemplateContext context)
    {
        // Like Ruby's Range#include?, any number between the bounds is included.
        if (value.Type != FluidValues.Number)
        {
            return new ValueTask<bool>(false);
        }

        var number = value.ToNumberValue(context);
        return new ValueTask<bool>(number >= Start && number <= End);
    }

    public override async IAsyncEnumerable<FluidValue> EnumerateAsync(TemplateContext context)
    {
        for (var i = 0; i < Count; i++)
        {
            context?.IncrementSteps();
            yield return this[i];
        }

        await Task.CompletedTask;
    }

    public override bool Equals(object obj)
    {
        return obj is FluidValue other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Start, End);
    }
}
