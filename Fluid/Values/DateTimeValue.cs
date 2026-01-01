using Fluid.Utils;
using System.Globalization;
using System.Text.Encodings.Web;

namespace Fluid.Values
{
    public sealed class DateTimeValue : FluidValue
    {
        private readonly DateTimeOffset _value;

        public DateTimeValue(DateTimeOffset value)
        {
            _value = value;
        }

        public DateTimeValue(DateTime value)
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

        public override bool ToBooleanValue()
        {
            return true;
        }

        public override decimal ToNumberValue()
        {
            return _value.Ticks;
        }

        public override string ToStringValue()
        {
            return _value.ToString("u", CultureInfo.InvariantCulture);
        }

        public override ValueTask WriteToAsync(IFluidOutput output, TextEncoder encoder, CultureInfo cultureInfo)
        {
            AssertWriteToParameters(output, encoder, cultureInfo);
            output.Write(_value.ToString("u", cultureInfo));
            return default;
        }

        public override object ToObjectValue()
        {
            return _value;
        }

        public override bool Equals(object obj)
        {
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
}
