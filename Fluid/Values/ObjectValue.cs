using System.Collections;
using System.Globalization;
using System.IO;
using System.Text.Encodings.Web;

namespace Fluid.Values
{
    public class ObjectValue : FluidValue
    {
        private readonly object _value;

        public ObjectValue(object value)
        {
            _value = value;
        }

        public override FluidValues Type => FluidValues.Object;

        public override bool Equals(FluidValue other)
        {
            if (other.IsNil())
            {
                switch (_value)
                {
                    case ICollection collection:
                        return collection.Count == 0;

                    case IEnumerable enumerable:
                        return !enumerable.GetEnumerator().MoveNext();
                }

                return false;
            }

            return other is ObjectValue && ((ObjectValue)other)._value == _value;
        }

        public override FluidValue GetValue(string name, TemplateContext context)
        {
            var value = context.MemberAccessStrategy.GetAccessor(_value, name)?.Get(_value, name, context);
            return FluidValue.Create(value);
        }

        public override FluidValue GetIndex(FluidValue index, TemplateContext context)
        {
            return GetValue(index.ToStringValue(), context);
        }

        public override bool ToBooleanValue()
        {
            return _value != null;
        }

        public override double ToNumberValue()
        {
            return 0;
        }

        public override void WriteTo(TextWriter writer, TextEncoder encoder, CultureInfo cultureInfo)
        {
            encoder.Encode(writer, _value.ToString());
        }

        public override string ToStringValue()
        {
            return _value.ToString();
        }

        public override object ToObjectValue()
        {
            return _value;
        }

        public override bool Equals(object other)
        {
            // The is operator will return false if null
            if (other is ObjectValue otherValue)
            {
                return _value.Equals(otherValue._value);
            }

            return false;
        }

        public override int GetHashCode()
        {
            return _value.GetHashCode();
        }
    }
}
