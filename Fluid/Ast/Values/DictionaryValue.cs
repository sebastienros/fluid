using System.Collections;
using System.IO;
using System.Text.Encodings.Web;

namespace Fluid.Ast.Values
{
    public class DictionaryValue : FluidValue, INamedSet
    {
        private readonly IDictionary _value;

        public DictionaryValue(IDictionary value)
        {
            _value = value;
        }

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
                    if (!otherDictionary._value.Contains(key))
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

        public FluidValue GetProperty(string name)
        {
            if (!_value.Contains(name))
            {
                return UndefinedValue.Instance;
            }

            return FluidValue.Create(_value[name]); 
        }

        public FluidValue GetIndex(FluidValue index)
        {
            return GetProperty(index.ToStringValue());
        }

        public override bool ToBooleanValue()
        {
            return false;
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
            return null;
        }

        public override object ToObjectValue()
        {
            return _value;
        }
    }
}
