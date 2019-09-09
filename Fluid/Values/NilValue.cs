using System.Globalization;
using System.IO;
using System.Text.Encodings.Web;

namespace Fluid.Values
{
    public sealed class NilValue : FluidValue
    {
        public static readonly NilValue Instance = new NilValue();
        public static readonly NilValue Empty = new NilValue();

        private NilValue()
        {
        }

        public override FluidValues Type => FluidValues.Nil;

        public override bool Equals(FluidValue other)
        {
            return other == Instance;
        }

        public override bool ToBooleanValue()
        {
            // Empty is a NilValue that is Truthy
            return ReferenceEquals(this, Empty);
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
            int hash = 27;
            hash = (13 * hash) + ToStringValue().GetHashCode();
            return hash;
        }
    }
}
