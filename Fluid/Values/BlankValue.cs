using System.Globalization;
using System.IO;
using System.Text.Encodings.Web;

namespace Fluid.Values
{
    public sealed class BlankValue : FluidValue
    {
        public static readonly BlankValue Instance = new BlankValue();

        private BlankValue()
        {
        }

        public override FluidValues Type => FluidValues.Empty;

        public override bool Equals(FluidValue other)
        {
            if (other == this) return true;
            if (other == BooleanValue.False) return true;
            if (other == EmptyValue.Instance) return true;
            if (other.ToObjectValue() == null) return true;
            if (other.Type == FluidValues.String && string.IsNullOrWhiteSpace(other.ToStringValue())) return true;

            return false;
        }

        public override bool ToBooleanValue()
        {
            // The only values that are falsy in Liquid are nil and false
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
