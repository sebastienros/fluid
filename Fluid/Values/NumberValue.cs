﻿using System.Globalization;
using System.IO;
using System.Text.Encodings.Web;

namespace Fluid.Values
{
    public class NumberValue : FluidValue
    {
        private readonly double _value;

        public NumberValue(double value)
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
            return _value != 0;
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
    }
}
