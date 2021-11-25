using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;

namespace Fluid.Values
{
    public sealed class ArrayValue : FluidValue
    {
        public static readonly ArrayValue Empty = new ArrayValue(Array.Empty<FluidValue>());

        private readonly FluidValue[] _value;

        public override FluidValues Type => FluidValues.Array;

        public ArrayValue(FluidValue[] value)
        {
            _value = value;
        }

        public ArrayValue(IEnumerable<FluidValue> value)
        {
            _value = value.ToArray();
        }

        internal ArrayValue(IList<FluidValue> value)
        {
            _value = value.ToArray();
        }

        public override bool Equals(FluidValue other)
        {
            if (other.IsNil())
            {
                return _value.Length == 0;
            }

            if (other is ArrayValue arrayValue)
            {
                if (_value.Length != arrayValue._value.Length)
                {
                    return false;
                }

                for (var i = 0; i < _value.Length; i++)
                {
                    var item = _value[i];
                    var otherItem = arrayValue._value[i];

                    if (!item.Equals(otherItem))
                    {
                        return false;
                    }
                }
            }
            else if (other.Type == FluidValues.Empty)
            {
                return _value.Length == 0;
            }
            
            return false;
        }

        protected override FluidValue GetValue(string name, TemplateContext context)
        {
            switch (name)
            {
                case "size":
                    return NumberValue.Create(_value.Length);

                case "first":
                    if (_value.Length > 0)
                    {
                        return _value[0];
                    }
                    break;

                case "last":
                    if (_value.Length > 0)
                    {
                        return _value[_value.Length - 1];
                    }
                    break;

            }

            return NilValue.Instance;
        }

        protected override FluidValue GetIndex(FluidValue index, TemplateContext context)
        {
            var i = (int)index.ToNumberValue();

            if (i >= 0 && i < _value.Length)
            {
                return FluidValue.Create(_value[i], context.Options);
            }

            return NilValue.Instance;
        }

        public override bool ToBooleanValue()
        {
            return true;
        }

        public override decimal ToNumberValue()
        {
            return _value.Length;
        }

        public FluidValue[] Values => _value;
        public override void WriteTo(TextWriter writer, TextEncoder encoder, CultureInfo cultureInfo)
        {
            AssertWriteToParameters(writer, encoder, cultureInfo);
            
            foreach (var v in _value)
            {
                writer.Write(v.ToStringValue());
            }
        }

        public override string ToStringValue()
        {
            return String.Join("", _value.Select(x => x.ToStringValue()));
        }

        public override object ToObjectValue()
        {
            return _value.Select(x => x.ToObjectValue()).ToArray();
        }

        public override bool Contains(FluidValue value)
        {
            return Array.IndexOf(_value, value) > -1;
        }

        public override IEnumerable<FluidValue> Enumerate(TemplateContext context)
        {
            return _value;
        }

        public override bool Equals(object other)
        {
            // The is operator will return false if null
            if (other is ArrayValue otherValue)
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
