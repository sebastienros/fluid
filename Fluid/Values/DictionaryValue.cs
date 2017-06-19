using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;

namespace Fluid.Values
{
    public class DictionaryValue : FluidValue
    {
        private readonly IDictionary<string, object> _value;

        public DictionaryValue(IDictionary value)
        {
            _value = new Dictionary<string, object>();
            foreach(string key in value.Keys)
            {
                _value.Add(key, value[key]);
            }
        }

        public DictionaryValue(IDictionary<string, object> value)
        {
            _value = value;
        }

        public DictionaryValue(IDictionary<string, string> value)
        {
            _value = value.ToDictionary(x => x.Key, y => (object)y);
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
                    if (!otherDictionary._value.ContainsKey(key))
                    {
                        return false;
                    }

                    var item = _value[key];
                    var otherItem = otherDictionary._value[key];

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
            return FluidValue.Create(value);
        }

        public override FluidValue GetIndex(FluidValue index, TemplateContext context)
        {
            var name = index.ToStringValue();

            if (!_value.ContainsKey(name))
            {
                return NilValue.Instance;
            }

            return FluidValue.Create(_value[name]);
        }

        public override bool ToBooleanValue()
        {
            return true;
        }

        public override double ToNumberValue()
        {
            return 0;
        }

        public override void WriteTo(TextWriter writer, TextEncoder encoder)
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
            foreach (var item in _value.Values)
            {
                if (item.Equals(value.ToObjectValue()))
                {
                    return true;
                }
            }

            return false;
        }

        public override IEnumerable<FluidValue> Enumerate()
        {
            foreach (var entry in _value)
            {
                yield return new ArrayValue(new FluidValue[] {
                    FluidValue.Create(entry.Key),
                    FluidValue.Create(entry.Value)
                });
            }
        }
    }
}
