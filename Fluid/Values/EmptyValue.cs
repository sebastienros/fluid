using System.Text.Encodings.Web;

namespace Fluid.Values;

public sealed class EmptyValue : FluidValue
{
    public static readonly EmptyValue Instance = new();

    private EmptyValue()
    {
    }

    public override FluidValues Type => FluidValues.Empty;

    public override bool Equals(FluidValue other)
    {
        // Built-in values are compared directly, any other value of the same kind compares itself.
        return other.Type switch
        {
            FluidValues.String => other is StringValue s ? s.Value == "" : other.Equals(this),
            // Like Liquid, a range is never empty.
            FluidValues.Array => other is RangeValue ? false : other is IFluidArray array ? array.Count == 0 : other.Equals(this),
            FluidValues.Dictionary => other is DictionaryValue dictionary ? dictionary.Count == 0 : other.Equals(this),
            _ => ReferenceEquals(other, this),
        };
    }

    public override bool ToBooleanValue(TemplateContext context)
    {
        return true;
    }

    public override decimal ToNumberValue(TemplateContext context)
    {
        return 0;
    }

    public override object ToObjectValue(TemplateContext context)
    {
        return "";
    }

    public override string ToStringValue(TemplateContext context)
    {
        return "";
    }

    public override bool IsNil()
    {
        return true;
    }

    public override ValueTask WriteToAsync(IFluidOutput output, TextEncoder encoder, TemplateContext context)
    {
        return default;
    }

    public override bool Equals(object obj)
    {
        // The is operator will return false if null
        return obj is NilValue;
    }

    public override int GetHashCode()
    {
        return GetType().GetHashCode();
    }
}
