using System;
using System.Globalization;
using System.IO;
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

        public override void WriteTo(TextWriter writer, TextEncoder encoder, CultureInfo cultureInfo)
        {
            AssertWriteToParameters(writer, encoder, cultureInfo);
            writer.Write(_value.ToString("u", cultureInfo));
        }

        public override object ToObjectValue()
        {
            return _value;
        }

        public override bool Equals(object other)
        {
            // The is operator will return false if null
            if (other is DateTimeOffset otherValue)
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
