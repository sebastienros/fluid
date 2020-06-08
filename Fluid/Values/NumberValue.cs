﻿using System.Globalization;
using System.IO;
using System.Text.Encodings.Web;

namespace Fluid.Values
{
    /// Numbers are stored as decimal values to handle the best possible precision.
    /// Decimals also have the capacity of retaining their precision across 
    /// operations:
    /// 1 * 2 = 2
    /// 1.0 * 2.0 = 2.00
    public sealed class NumberValue : FluidValue
    {
        public static readonly NumberValue Zero = new NumberValue(0M);
        private readonly decimal _value;

        private NumberValue(decimal value)
        {
            _value = value;
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
            if (writer == null)
            {
                ExceptionHelper.ThrowArgumentNullException(nameof(writer));
            }

            if (encoder == null)
            {
                ExceptionHelper.ThrowArgumentNullException(nameof(encoder));
            }

            if (cultureInfo == null)
            {
                ExceptionHelper.ThrowArgumentNullException(nameof(cultureInfo));
            }

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
