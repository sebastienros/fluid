using System.Globalization;
using System.Text.Encodings.Web;

namespace Fluid.Values;

public sealed class BlankValue : FluidValue
{
    public static readonly BlankValue Instance = new BlankValue();

    private BlankValue()
    {
    }

    public override FluidValues Type => FluidValues.Blank;

    public override bool Equals(FluidValue other)
    {
        if (other == this) return true;
        if (other == BooleanValue.False) return true;
        if (other == EmptyValue.Instance) return false;
        if (other.IsNil()) return true;

        // Built-in values are compared directly, any other value of the same kind compares itself.
        return other.Type switch
        {
            FluidValues.String => other is StringValue s ? string.IsNullOrWhiteSpace(s.Value) : other.Equals(this),
            FluidValues.Array => other is IFluidArray array ? array.Count == 0 : other.Equals(this),
            FluidValues.Dictionary => other is DictionaryValue dictionary ? dictionary.Count == 0 : other.Equals(this),
            _ => false,
        };
    }

    public override bool ToBooleanValue(TemplateContext context)
    {
        // The only values that are falsy in Liquid are nil and false
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
