using System.Globalization;
using System.IO;
using System.Text.Encodings.Web;

namespace Fluid.Values
{
    public class NumberValue : FluidValue
    {
        private readonly double _value;
        private readonly bool _isIntegral;

        public NumberValue(double value, bool isIntegral = false)
        {
            _value = value;
            _isIntegral = isIntegral;
        }

        public override FluidValues Type => FluidValues.Number;

        public override bool Equals(FluidValue other)
        {
            return _value == other.ToNumberValue();
        }

        public override bool ToBooleanValue()
        {
            return true;
        }

        public override double ToNumberValue()
        {
            return _value;
        }

        public override string ToStringValue()
        {
            return _value.ToString(CultureInfo.InvariantCulture);
        }

        public override void WriteTo(TextWriter writer, TextEncoder encoder, CultureInfo cultureInfo)
        {
            encoder.Encode(writer, _value.ToString(cultureInfo));
        }

        public override object ToObjectValue()
        {
            return _value;
        }

        public override bool Equals(object other)
        {
            // The is operator will return false if null
            if (other is NumberValue otherValue)
            {
                return _value.Equals(otherValue._value);
            }

            return false;
        }

        public bool IsIntegral => _isIntegral;

        public override int GetHashCode()
        {
            return _value.GetHashCode();
        }
    }
}
