using System;
using System.IO;
using System.Text.Encodings.Web;

namespace Fluid.Ast.Values
{
    public class StringValue : FluidValue, INamedSet
    {
        private readonly string _value;

        public StringValue(string value)
        {
            _value = value;
        }

        public override bool Equals(FluidValue other)
        {
            if (other == EmptyValue.Instance)
            {   
                return _value.Length == 0;
            }

            return _value == other.ToStringValue();
        }

        public FluidValue GetIndex(FluidValue index)
        {
            var i = Convert.ToInt32(index.ToNumberValue());

            if (i < _value.Length)
            {
                return Create(_value[i]);
            }

            return UndefinedValue.Instance;
        }

        public FluidValue GetValue(string name)
        {
            return UndefinedValue.Instance;
        }

        public override bool ToBooleanValue()
        {
            return _value != null;
        }

        public override double ToNumberValue()
        {
            return 0;
        }

        public override string ToStringValue()
        {
            return _value;
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
