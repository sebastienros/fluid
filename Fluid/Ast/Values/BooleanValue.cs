using System.IO;
using System.Text.Encodings.Web;

namespace Fluid.Ast.Values
{
    public class BooleanValue : FluidValue
    {
        public static BooleanValue False = new BooleanValue(false);
        public static BooleanValue True = new BooleanValue(true);

        private readonly bool _value;

        public BooleanValue(bool value)
        {
            _value = value;
        }

        public override bool Equals(FluidValue other)
        {
            return _value == other.ToBooleanValue();
        }

        public override bool ToBooleanValue()
        {
            return _value;
        }

        public override double ToNumberValue()
        {
            return _value ? 1 : 0;
        }

        public override string ToStringValue()
        {
            return _value ? "true" : "false";
        }

        public override void WriteTo(TextWriter writer, TextEncoder encoder)
        {
            writer.Write(encoder.Encode(ToStringValue()));
        }

        public override object ToObjectValue()
        {
            return _value;
        }
    }
}
