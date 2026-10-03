using System.Globalization;
using System.Text.Encodings.Web;

namespace Fluid.Values
{
    public abstract class BaseNilValue : FluidValue
    {
        public override FluidValues Type => FluidValues.Nil;

        public override ValueTask<FluidValue> GetValueAsync(string name, TemplateContext context)
        {
            // Nil values have a size of 0
            if (name == "size")
            {
                return NumberValue.Zero;
            }
            return NilValue.Instance;
        }

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

        public override ValueTask WriteToAsync(IFluidOutput output, TextEncoder encoder, CultureInfo cultureInfo)
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

        private NilValue()
        {
        }
    }
}
