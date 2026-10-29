using System.Text.Encodings.Web;

namespace Fluid.Values;

public sealed class BooleanValue : FluidValue
{
    public static readonly BooleanValue False = new BooleanValue(false);
    public static readonly BooleanValue True = new BooleanValue(true);

    private static readonly object BoxedTrue = true;
    private static readonly object BoxedFalse = false;

    private readonly bool _value;

    private BooleanValue(bool value)
    {
        _value = value;
    }

    public override FluidValues Type => FluidValues.Boolean;

    public static BooleanValue Create(bool value)
    {
        return value ? True : False;
    }

    public override bool Equals(FluidValue other)
    {
        // blank == false -> true
        if (other.Type == FluidValues.Blank) return !_value;

        if (other.Type != FluidValues.Boolean)
        {
            return false;
        }

        // Built-in values are compared directly, any other value of the same kind compares itself.
        return other is BooleanValue b ? _value == b._value : other.Equals(this);
    }

    /// <summary>
    /// Gets the boolean, which doesn't depend on a <see cref="TemplateContext"/>.
    /// </summary>
    internal bool Value => _value;

    public override bool ToBooleanValue(TemplateContext context)
    {
        return _value;
    }

    public override decimal ToNumberValue(TemplateContext context)
    {
        return _value ? 1 : 0;
    }

    public override string ToStringValue(TemplateContext context)
    {
        return _value ? "true" : "false";
    }

    public override ValueTask WriteToAsync(IFluidOutput output, TextEncoder encoder, TemplateContext context)
    {
        AssertWriteToParameters(output, encoder, context);
        output.Write(encoder, _value ? "true" : "false");
        return default;
    }

    public override object ToObjectValue(TemplateContext context)
    {
        return _value ? BoxedTrue : BoxedFalse;
    }

    public override bool Equals(object obj)
    {
        // The is operator will return false if null
        if (obj is BooleanValue otherValue)
        {
            return _value.Equals(otherValue._value);
        }

        return false;
    }

    public override int GetHashCode()
    {
        return _value.GetHashCode();
    }
}
