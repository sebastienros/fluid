using System.Globalization;
using System.Text.Encodings.Web;

namespace Fluid.Values
{
    public abstract class BaseNilValue : FluidValue
    {
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

        [Obsolete("WriteTo is obsolete, prefer the WriteToAsync method.")]
        public override void WriteTo(TextWriter writer, TextEncoder encoder, CultureInfo cultureInfo)
        {
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

    public sealed class NilValue : BaseNilValue
    {
        public static readonly NilValue Instance = new NilValue(); // a variable that is not defined, or the nil keyword

        [Obsolete("Use EmptyValue.Instance instead.")]
        public static readonly EmptyValue Empty = EmptyValue.Instance;

        private NilValue()
        {
        }
    }
}
