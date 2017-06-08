using System;
using System.IO;
using System.Text.Encodings.Web;

namespace Fluid.Ast.Values
{
    public class BooleanValue : FluidValue
    {
        private readonly bool _value;

        public BooleanValue(bool value)
        {
            _value = value;
        }

        public override FluidValue Add(FluidValue other)
        {
            throw new NotSupportedException("Can't add a boolean value");
        }

        public override FluidValue Equals(FluidValue other)
        {
            return new BooleanValue(_value == other.ToBoolean());
        }

        public override bool ToBoolean()
        {
            return _value;
        }

        public override double ToNumber()
        {
            return _value ? 1 : 0;
        }

        public override void WriteTo(TextWriter writer, TextEncoder encoder)
        {
            writer.Write(encoder.Encode(_value.ToString()));
        }
    }
}
