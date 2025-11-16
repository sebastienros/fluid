using System.Globalization;
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

        public override bool ToBooleanValue(TemplateContext context)
        {
            // The only values that are falsy in Liquid are nil and false
            return true;
        }

        public override decimal ToNumberValue()
        {
            return 0;
        }

        public override decimal ToNumberValue(TemplateContext context)
        {
            return 0;
        }

        public override object ToObjectValue()
        {
            return "";
        }

        public override object ToObjectValue(TemplateContext context)
        {
            return "";
        }

        public override string ToStringValue()
        {
            return "";
        }

        public override string ToStringValue(TemplateContext context)
        {
            return "";
        }

        public override bool IsNil()
        {
            return true;
        }

        public override ValueTask WriteToAsync(TextWriter writer, TextEncoder encoder, CultureInfo cultureInfo)
        {
            return default;
        }

        public override bool Equals(object obj)
        {
            // The is operator will return false if null
            return obj is NilValue;
        }

        public override int GetHashCode()
        {
            return GetType().GetHashCode();
        }
    }
}
