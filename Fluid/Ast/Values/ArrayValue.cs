using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;

namespace Fluid.Ast.Values
{
    public class ArrayValue : FluidValue, INamedSet
    {
        private readonly IList _value;

        public ArrayValue(IList value)
        {
            _value = value;
        }

        public ArrayValue(IEnumerable value)
        {
            _value = new List<object>();
            
            foreach(var item in value)
            {
                _value.Add(item);
            }
        }

        public override bool Equals(FluidValue other)
        {
            if (other == EmptyValue.Instance)
            {
                return _value.Count == 0;
            }

            if (other is ArrayValue arrayValue)
            {
                if (_value.Count != arrayValue._value.Count)
                {
                    return false;
                }

                for (var i=0; i<_value.Count; i++)
                {
                    var item = _value[i];
                    var otherItem = arrayValue._value[i];

                    if (!item.Equals(otherItem))
                    {
                        return false;
                    }
                }
            }

            return false;
        }

        public FluidValue GetValue(string name)
        {
            if (name == "size")
            {
                return new NumberValue(_value.Count);
            }

            return NilValue.Instance;
        }

        public FluidValue GetIndex(FluidValue index)
        {
            var i = (int)index.ToNumberValue();

            if (i >= 0 && i < _value.Count)
            {
                return FluidValue.Create(_value[i]);
            }

            return NilValue.Instance;
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

        public bool Contains(FluidValue value)
        {
            foreach (var item in _value)
            {
                if (item.Equals(value.ToObjectValue()))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
