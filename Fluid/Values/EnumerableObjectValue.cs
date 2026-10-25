using Fluid.Utils;
using System.Collections;
using System.Globalization;
using System.Text.Encodings.Web;

namespace Fluid.Values;

internal sealed class EnumerableObjectValue : ObjectValueBase, IFluidArray
{
    public static EnumerableObjectValue Create(IEnumerable value, TemplateOptions options)
        => new EnumerableObjectValue(value, options);

    private EnumerableObjectValue(IEnumerable value, TemplateOptions options) : base(value)
    {
        var values = new List<FluidValue>();

        foreach (var item in value)
        {
            values.Add(FluidValue.Create(item, options));
        }

        Values = values;
    }

    public IReadOnlyList<FluidValue> Values { get; }

    public override FluidValues Type => FluidValues.Array;

    int IFluidArray.Count => Values.Count;

    FluidValue IFluidArray.this[int index] => Values[index];

    public override ValueTask<FluidValue> GetValueAsync(string name, TemplateContext context)
    {
        switch (name)
        {
            case "size":
                return NumberValue.Create(Values.Count);
            case "first":
                return Values.Count > 0 ? Values[0] : NilValue.Instance;
            case "last":
                return Values.Count > 0 ? Values[Values.Count - 1] : NilValue.Instance;
            default:
                return base.GetValueAsync(name, context);
        }
    }

    public override ValueTask<FluidValue> GetIndexAsync(FluidValue index, TemplateContext context)
    {
        // An index too large for an int can't name an item.
        if (!NumberConversions.TryTruncateToInt32(index.ToNumberValue(context), out var i))
        {
            return NilValue.Instance;
        }


        if (i < 0)
        {
            i = Values.Count + i;
        }

        return i >= 0 && i < Values.Count
            ? Values[i]
            : NilValue.Instance;
    }

    public override bool ToBooleanValue(TemplateContext context) => true;

    public override decimal ToNumberValue(TemplateContext context) => Values.Count;

    public override bool Equals(FluidValue other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (other.IsNil())
        {
            return Values.Count == 0;
        }

        if (other is IFluidArray otherValues)
        {
            using var scope = RecursiveComparisonGuard.Enter(this, other);

            if (Values.Count != otherValues.Count)
            {
                return false;
            }

            for (var i = 0; i < Values.Count; i++)
            {
                if (!Values[i].Equals(otherValues[i] ?? NilValue.Instance))
                {
                    return false;
                }
            }

            return true;
        }

        return other.Type == FluidValues.Empty && Values.Count == 0;
    }

    public override string ToStringValue(TemplateContext context)
    {
        return ArrayValue.ConcatStringValues(Values, context);
    }

    public override object ToObjectValue(TemplateContext context) => Value;

    public override bool Equals(object obj) => obj is FluidValue other && Equals(other);

    public override int GetHashCode()
    {
        using var scope = RecursiveValueGuard.Enter(this);
        var hashCode = new HashCode();

        foreach (var value in Values)
        {
            hashCode.Add(value);
        }

        return hashCode.ToHashCode();
    }

    public override ValueTask WriteToAsync(IFluidOutput output, TextEncoder encoder, TemplateContext context)
    {
        AssertWriteToParameters(output, encoder, context);

        return ArrayValue.WriteItemsAsync(this, Values, output, encoder, context);
    }

    public override async IAsyncEnumerable<FluidValue> EnumerateAsync(TemplateContext context)
    {
        foreach (var value in Values)
        {
            context?.IncrementSteps();
            yield return value;
        }

        await Task.CompletedTask;
    }
}
