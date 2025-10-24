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
            
            try
            {
                // Attempt normal conversion - implicit conversion uses local timezone for Unspecified kind
                _value = value;
            }
            catch (ArgumentOutOfRangeException)
            {
                // If conversion fails due to offset overflow, use the appropriate boundary value
                // This happens when the UTC representation would be outside the valid range
                if (value < DateTime.MinValue.AddDays(1))
                {
                    // Value is close to MinValue and offset caused underflow
                    _value = DateTimeOffset.MinValue;
                }
                else
                {
                    // Value is close to MaxValue and offset caused overflow
                    _value = DateTimeOffset.MaxValue;
                }
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

        [Obsolete("WriteTo is obsolete, prefer the WriteToAsync method.")]
        public override void WriteTo(TextWriter writer, TextEncoder encoder, CultureInfo cultureInfo)
        {
            AssertWriteToParameters(writer, encoder, cultureInfo);
            writer.Write(_value.ToString("u", cultureInfo));
        }

        public override ValueTask WriteToAsync(TextWriter writer, TextEncoder encoder, CultureInfo cultureInfo)
        {
            AssertWriteToParameters(writer, encoder, cultureInfo);
            var task = writer.WriteAsync(_value.ToString("u", cultureInfo));

            if (task.IsCompletedSuccessfully())
            {
                return default;
            }

            return Awaited(task);

            static async ValueTask Awaited(Task t)
            {
                await t;
                return;
            }
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
