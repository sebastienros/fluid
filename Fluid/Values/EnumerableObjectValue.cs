using System.Collections;
using System.Globalization;
using System.Text.Encodings.Web;

namespace Fluid.Values;

internal sealed class EnumerableObjectValue : ObjectValueBase
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

    internal static IReadOnlyList<FluidValue> GetMaterializedValues(FluidValue value)
    {
        return value switch
        {
            ArrayValue array => array.Values,
            EnumerableObjectValue enumerable => enumerable.Values,
            _ => null
        };
    }

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
        var i = (int)index.ToNumberValue();

        if (i < 0)
        {
            i = Values.Count + i;
        }

        return i >= 0 && i < Values.Count
            ? Values[i]
            : NilValue.Instance;
    }

    public override bool ToBooleanValue() => true;

    public override decimal ToNumberValue() => Values.Count;

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

        var otherValues = GetMaterializedValues(other);
        if (otherValues is not null)
        {
            using var scope = RecursiveComparisonGuard.Enter(this, other);

            if (Values.Count != otherValues.Count)
            {
                return false;
            }

            for (var i = 0; i < Values.Count; i++)
            {
                if (!Values[i].Equals(otherValues[i]))
                {
                    return false;
                }
            }

            return true;
        }

        return other.Type == FluidValues.Empty && Values.Count == 0;
    }

    public override string ToStringValue()
    {
        return String.Join("", Values.Select(x => x.ToStringValue()));
    }

    public override object ToObjectValue() => Value;

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

    public override ValueTask WriteToAsync(IFluidOutput output, TextEncoder encoder, CultureInfo cultureInfo)
    {
        AssertWriteToParameters(output, encoder, cultureInfo);

        foreach (var value in Values)
        {
            output.Write(value.ToStringValue());
        }

        return default;
    }

    public override IEnumerable<FluidValue> Enumerate(TemplateContext context)
    {
        foreach (var value in Values)
        {
            context?.IncrementSteps();
            yield return value;
        }
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
