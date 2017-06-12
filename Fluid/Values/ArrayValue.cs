using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;

namespace Fluid.Values
{
    public class ArrayValue : FluidValue
    {
        private readonly IList _value;

        public override FluidValues Type => FluidValues.Array;

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

        public override FluidValue GetValue(string name, TemplateContext context)
        {
            switch (name)
            {
                case "size":
                    return new NumberValue(_value.Count);

                case "first":
                    if (_value.Count > 0)
                    {
                        return FluidValue.Create(_value[0]);
                    }
                    break;

                case "last":
                    if (_value.Count > 0)
                    {
                        return FluidValue.Create(_value[_value.Count - 1]);
                    }
                    break;

            }

            return NilValue.Instance;
        }

        public override FluidValue GetIndex(FluidValue index, TemplateContext context)
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
            return _value.Contains(value.ToObjectValue());
        }

        public override IEnumerable<FluidValue> Enumerate()
        {
            foreach (var item in _value)
            {
                yield return FluidValue.Create(item);
            }
        }
    }
}
