using System.Globalization;
using System.IO;
using System.Text.Encodings.Web;

namespace Fluid.Values
{
    public class NumberValue : FluidValue
    {
        public static readonly NumberValue Zero = new NumberValue(0);

        // We can cache most common values, doubles are used in indexing too at times so we also cache
        // integer values converted to doubles
        private const int NumbersMax = 1024 * 10;
        private static readonly NumberValue[] _doubleToValue = new NumberValue[NumbersMax];
        private static readonly NumberValue[] _intToValue = new NumberValue[NumbersMax];
        private static readonly NumberValue NegativeOneIntegral = new NumberValue(-1, true);
        private static readonly NumberValue NegativeOneDouble = new NumberValue(-1, false);
        private readonly double _value;

        static NumberValue()
        {
            for (var i = 0; i < NumbersMax; i++)
            {
                _intToValue[i] = new NumberValue(i, true);
                _doubleToValue[i] = new NumberValue(i, false);
            }
        }

        private NumberValue(double value, bool isIntegral = false)
        {
            _value = value;
            IsIntegral = isIntegral;
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

        public bool IsIntegral { get; private set; }

        public override int GetHashCode()
        {
            return _value.GetHashCode();
        }

        public static NumberValue Create(double value, bool integral = false)
        {
            if (value >= 0 && value < NumbersMax && (int)value == value)
            {
                if (integral)
                {
                    return _intToValue[(int)value];
                }
                else
                {
                    return _doubleToValue[(int)value];
                }
            }
            else
            {
                if (value == -1)
                {
                    return integral ? NegativeOneIntegral : NegativeOneIntegral;
                }
            }

            return new NumberValue(value);
        }

        public static NumberValue Create(int value, bool integral = false)
        {
            if (value < NumbersMax && value >= 0)
            {
                if (integral)
                {
                    return _intToValue[value];
                }
                else
                {
                    return _doubleToValue[value];
                }
            }
            else
            {
                if (value == -1)
                {
                    return integral ? NegativeOneIntegral : NegativeOneIntegral;
                }

            }

            return new NumberValue(value);
        }

    }
}
