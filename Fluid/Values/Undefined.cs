﻿using System.Globalization;
using System.IO;
using System.Text.Encodings.Web;

namespace Fluid.Values
{
    public class UndefinedValue : FluidValue
    {
        public static readonly UndefinedValue Instance = new UndefinedValue();

        private UndefinedValue()
        {
        }

        public override FluidValues Type => FluidValues.Undefined;

        public override bool Equals(FluidValue other)
        {
            return other == Instance;
        }

        public override bool ToBooleanValue()
        {
            return false;
        }

        public override double ToNumberValue()
        {
            return 0;
        }

        public override object ToObjectValue()
        {
            return null;
        }

        public override string ToStringValue()
        {
            return "";
        }

        public override bool IsUndefined()
        {
            return true;
        }

        public override void WriteTo(TextWriter writer, TextEncoder encoder, CultureInfo cultureInfo)
        {
        }
    }
}
