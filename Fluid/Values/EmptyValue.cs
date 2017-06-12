using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;

namespace Fluid.Values
{
    public class EmptyValue : FluidValue
    {
        public static readonly EmptyValue Instance = new EmptyValue();

        private EmptyValue()
        {
        }

        public override FluidValues Type => FluidValues.Empty;

        public override bool Equals(FluidValue other)
        {
            switch (other)
            {
                case StringValue stringValue:
                    return stringValue.Equals(this);

                case ObjectValue objectValue:
                    return objectValue.Equals(this);
            }

            return false;
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
            return null;
        }

        public override bool IsUndefined()
        {
            return true;
        }

        public override void WriteTo(TextWriter writer, TextEncoder encoder)
        {
        }
    }
}
