using System.Globalization;
using System.IO;
using System.Text.Encodings.Web;

namespace Fluid.Values
{
    public sealed class EmptyValue : FluidValue
    {
        public static readonly EmptyValue Instance = new EmptyValue();

        private EmptyValue()
        {
        }

        public override FluidValues Type => FluidValues.Empty;

        public override bool Equals(FluidValue other)
        {
            if (other.Type == FluidValues.String && other.ToStringValue() == "") return true;
            if (other.Type == FluidValues.Array && other.ToNumberValue() == 0) return true;
            if (other.Type == FluidValues.Dictionary &&other.ToNumberValue() == 0) return true;
            if (other == BlankValue.Instance) return true;
            if (other == EmptyValue.Instance) return true;
            if (other == NilValue.Instance) return false;

            return false;
        }

        public override bool ToBooleanValue()
        {
            return true;
        }

        public override decimal ToNumberValue()
        {
            return 0;
        }

        public override object ToObjectValue()
        {
            return "";
        }

        public override string ToStringValue()
        {
            return "";
        }

        public override bool IsNil()
        {
            return true;
        }

        public override void WriteTo(TextWriter writer, TextEncoder encoder, CultureInfo cultureInfo)
        {
        }

        public override bool Equals(object other)
        {
            // The is operator will return false if null
            return other is NilValue;
        }

        public override int GetHashCode()
        {
            return GetType().GetHashCode();
        }
    }
}
