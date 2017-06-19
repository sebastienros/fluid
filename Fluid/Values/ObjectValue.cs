using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
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
            if (other == EmptyValue.Instance)
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
            var value = context.MemberAccessStrategy.Get(_value, name);
            return FluidValue.Create(value);
        }

        public override FluidValue GetIndex(FluidValue index, TemplateContext context)
        {
            // Indexers are not exposed for security.
            return NilValue.Instance;
        }

        public override bool ToBooleanValue()
        {
            return _value != null;
        }

        public override double ToNumberValue()
        {
            return 0;
        }

        public override void WriteTo(TextWriter writer, TextEncoder encoder)
        {
            encoder.Encode(writer, ToStringValue());
        }

        public override string ToStringValue()
        {
            return _value.ToString();
        }

        public override object ToObjectValue()
        {
            return _value;
        }
    }
}
