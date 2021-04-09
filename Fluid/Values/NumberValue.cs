using System;
using System.Globalization;
using System.IO;
using System.Text.Encodings.Web;

namespace Fluid.Values
{
    /// Numbers are stored as decimal values to handle the best possible precision.
    /// Decimals also have the capacity of retaining their precision across
    /// operations:
    /// 1 * 2 = 2
    /// 1.0 * 2.0 = 2.00
    public sealed class NumberValue : FluidValue, IEquatable<NumberValue>
    {
        public static readonly NumberValue Zero = new NumberValue(0M);

        private static readonly NumberValue[] IntToString = new NumberValue[1024];

        private readonly decimal _value;

        static NumberValue()
        {
            for (var i = 0; i < IntToString.Length; ++i)
            {
                IntToString[i] = new NumberValue(i);
            }
        }

        private NumberValue(decimal value)
        {
            _value = value;
        }

        public override FluidValues Type => FluidValues.Number;

        public override bool Equals(FluidValue other)
        {
            // Delegating other types 
            if (other == BlankValue.Instance || other == NilValue.Instance || other == EmptyValue.Instance)
            {
                return false;
            }

            return _value == other.ToNumberValue();
        }

        public override bool ToBooleanValue()
        {
            return true;
        }

        public override decimal ToNumberValue()
        {
            return _value;
        }

        public override string ToStringValue()
        {
            return _value.ToString(CultureInfo.InvariantCulture);
        }

        public override void WriteTo(TextWriter writer, TextEncoder encoder, CultureInfo cultureInfo)
        {
            AssertWriteToParameters(writer, encoder, cultureInfo);
            writer.Write(encoder.Encode(_value.ToString(cultureInfo)));
        }

        public override object ToObjectValue()
        {
            return _value;
        }

        public override bool Equals(object other)
        {
            return other is NumberValue n && Equals(n);
        }

        public bool Equals(NumberValue other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return _value == other._value;
        }

        public override int GetHashCode()
        {
            return _value.GetHashCode();
        }

        public static NumberValue Create(string value)
        {
            if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
            {
                return Create(d);
            }

            return Zero;
        }

        public static NumberValue Create(decimal value)
        {
            return new NumberValue(value);
        }

        internal static NumberValue Create(uint value)
        {
            var temp = IntToString;
            if (value < (uint) temp.Length)
            {
                return temp[value];
            }
            return new NumberValue(value);
        }

        internal static NumberValue Create(int value)
        {
            var temp = IntToString;
            if (value >= 0 && value < temp.Length)
            {
                return temp[value];
            }
            return new NumberValue(value);
        }

        public static int GetScale(decimal value)
        {
            if (value == 0)
            {
                return 0;
            }

            int[] bits = decimal.GetBits(value);

            return (int) ((bits[3] >> 16) & 0x7F);
        }
    }
}
