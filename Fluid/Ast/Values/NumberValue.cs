using System.IO;
using System.Text.Encodings.Web;

namespace Fluid.Ast.Values
{
    public class NumberValue : FluidValue
    {
        private readonly double _value;

        public NumberValue(double value)
        {
            _value = value;
        }

        public override FluidValue Add(FluidValue other)
        {
            return new NumberValue(_value + other.ToNumber());
        }

        public override FluidValue Equals(FluidValue other)
        {
            return new BooleanValue(_value == other.ToNumber());
        }

        public override bool ToBoolean()
        {
            throw new System.NotImplementedException();
        }

        public override double ToNumber()
        {
            throw new System.NotImplementedException();
        }

        public override void WriteTo(TextWriter writer, TextEncoder encoder)
        {
            writer.Write(encoder.Encode(_value.ToString()));
        }
    }
}
