using Fluid.Utils;
using System.Globalization;
using System.Text.Encodings.Web;

namespace Fluid.Values;

public sealed class DateTimeValue : FluidValue
{
    private readonly DateTimeOffset _value;

    public static DateTimeValue Create(DateTimeOffset value) => new DateTimeValue(value);

    public static DateTimeValue Create(DateTime value) => new DateTimeValue(value);

    private DateTimeValue(DateTimeOffset value)
    {
        _value = value;
    }

    private DateTimeValue(DateTime value)
    {
        // Handle edge cases where DateTime cannot be safely converted to DateTimeOffset
        // with local timezone offset due to overflow (e.g., DateTime.MinValue with positive offset)

        // Check if the value is within one day of the boundaries where overflow might occur
        if (value <= DateTime.MinValue.AddDays(1))
        {
            // Value is close to MinValue - attempt conversion with try-catch
            try
            {
                _value = value;
            }
            catch (ArgumentOutOfRangeException)
            {
                // Offset caused underflow - use minimum boundary
                _value = DateTimeOffset.MinValue;
            }
        }
        else if (value >= DateTime.MaxValue.AddDays(-1))
        {
            // Value is close to MaxValue - attempt conversion with try-catch
            try
            {
                _value = value;
            }
            catch (ArgumentOutOfRangeException)
            {
                // Offset caused overflow - use maximum boundary
                _value = DateTimeOffset.MaxValue;
            }
        }
        else
        {
            // Normal case - direct conversion without try-catch overhead
            _value = value;
        }
    }

    public override FluidValues Type => FluidValues.DateTime;

    public override bool Equals(FluidValue other)
    {
        if (other.IsNil())
        {
            return false;
        }

        if (other.Type != FluidValues.DateTime)
        {
            return false;
        }

        return _value.Equals(((DateTimeValue)other)._value);
    }

    public override bool ToBooleanValue(TemplateContext context)
    {
        return true;
    }

    public override decimal ToNumberValue(TemplateContext context)
    {
        return _value.Ticks;
    }

    public override string ToStringValue(TemplateContext context)
    {
        return _value.ToString("u", CultureInfo.InvariantCulture);
    }

    public override ValueTask WriteToAsync(IFluidOutput output, TextEncoder encoder, TemplateContext context)
    {
        AssertWriteToParameters(output, encoder, context);
        output.Write(_value.ToString("u", context.CultureInfo));
        return default;
    }

    public override object ToObjectValue(TemplateContext context)
    {
        return _value;
    }

    public override bool Equals(object obj)
    {
        if (obj is FluidValue fluidValue)
        {
            return Equals(fluidValue);
        }

        // The is operator will return false if null
        if (obj is DateTimeOffset otherValue)
        {
            return _value.Equals(otherValue);
        }

        return false;
    }

    public override int GetHashCode()
    {
        return _value.GetHashCode();
    }
}
