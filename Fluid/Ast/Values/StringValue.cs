using System.IO;
using System.Text.Encodings.Web;

namespace Fluid.Ast.Values
{
    public class StringValue : FluidValue
    {
        private readonly string _value;

        public StringValue(string value)
        {
            _value = value;
        }

        public override FluidValue Add(FluidValue other)
        {
            return new StringValue(_value + other.ToString());
        }

        public override FluidValue Equals(FluidValue other)
        {
            return new BooleanValue(_value == other.ToString());
        }

        public override bool ToBoolean()
        {
            return string.IsNullOrEmpty(_value);
        }

        public override double ToNumber()
        {
            return 0;
        }

        public override void WriteTo(TextWriter writer, TextEncoder encoder)
        {
            writer.Write(encoder.Encode(_value));
        }
    }
}
