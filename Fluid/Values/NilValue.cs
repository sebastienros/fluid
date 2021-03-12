using System.Globalization;
using System.IO;
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
            if (this == Empty)
            {
                return other == Empty
                    || other == Instance
                    || other == StringValue.Blank
                    || other.Type == FluidValues.String && other.ToStringValue() == ""
                    ;
            }
            else if (this == Instance)
            {
                return other == Instance
                    || other == Empty
                    || other == StringValue.Blank
                    ;
            }

            return false;
        }

        public override bool ToBooleanValue()
        {
            // Empty is a NilValue that is Truthy
            return ReferenceEquals(this, Empty);
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
