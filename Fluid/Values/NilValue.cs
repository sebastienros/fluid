using System.Globalization;
using System.Text.Encodings.Web;

namespace Fluid.Values
{
    public sealed class NilValue : FluidValue
    {
        public static readonly NilValue Instance = new NilValue(); // a variable that is not defined, or the nil keyword
        public static readonly NilValue Empty = new NilValue(); // the empty keyword

        private NilValue()
        {
        }

        public override FluidValues Type => FluidValues.Nil;

        public override bool Equals(FluidValue other)
        {
            if (other == EmptyValue.Instance) return false;

            if (other == NilValue.Instance
                || other == BlankValue.Instance)
            {
                return true;
            }

            return other.ToObjectValue() == null;
        }

        public override bool ToBooleanValue()
        {
            return false;
        }

        public override decimal ToNumberValue()
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

        public override bool IsNil()
        {
            return true;
        }

        public override void WriteTo(TextWriter writer, TextEncoder encoder, CultureInfo cultureInfo)
        {
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
