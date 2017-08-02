using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;

namespace Fluid.Values
{
    public class DictionaryValue : FluidValue
    {
        private readonly IFluidIndexable _value;

        public DictionaryValue(IFluidIndexable value)
        {
            _value = value;
        }

        public override FluidValues Type => FluidValues.Dictionary;

        public override bool Equals(FluidValue other)
        {
            if (other == EmptyValue.Instance)
            {
                return _value.Count == 0;
            }

            if (other is DictionaryValue otherDictionary)
            {
                if (_value.Count != otherDictionary._value.Count)
                {
                    return false;
                }

                foreach (var key in _value.Keys)
                {
                    if (!otherDictionary._value.TryGetValue(key, out var otherItem))
                    {
                        return false;
                    }

                    _value.TryGetValue(key, out var item);

                    if (!item.Equals(otherItem))
                    {
                        return false;
                    }
                }
            }

            return false;
        }

        public override FluidValue GetValue(string name, TemplateContext context)
        {
            if (name == "size")
            {
                return new NumberValue(_value.Count);
            }

            var value = context.MemberAccessStrategy.Get(_value, name);

            if (value == null)
            {
                return GetIndex(new StringValue(name), context);
            }

            return FluidValue.Create(value);
        }

        public override FluidValue GetIndex(FluidValue index, TemplateContext context)
        {
            var name = index.ToStringValue();

            if (!_value.TryGetValue(name, out var value))
            {
                return NilValue.Instance;
            }

            return value;
        }

        public override bool ToBooleanValue()
        {
            return true;
        }

        public override double ToNumberValue()
        {
            return 0;
        }

        public override void WriteTo(TextWriter writer, TextEncoder encoder, CultureInfo cultureInfo)
        {
        }

        public override string ToStringValue()
        {
            return "";
        }

        public override object ToObjectValue()
        {
            return _value;
        }

        public override bool Contains(FluidValue value)
        {
            foreach (var key in _value.Keys)
            {
                if (_value.TryGetValue(key, out var item) && item.Equals(value.ToObjectValue()))
                {
                    return true;
                }
            }

            return false;
        }

        public override IEnumerable<FluidValue> Enumerate()
        {
            foreach (var key in _value.Keys)
            {
                _value.TryGetValue(key, out var value);
                yield return new ArrayValue(new FluidValue[] {
                    new StringValue(key),
                    value
                });
            }
        }
    }
}
