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
            // The 'empty' comparison is delegated to each value type
            return other.Equals(this);
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

        public override void WriteTo(TextWriter writer, TextEncoder encoder)
        {
        }
    }
}
