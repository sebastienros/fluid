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
        private readonly List<FluidValue> _value;

        public override FluidValues Type => FluidValues.Array;

        public ArrayValue(List<FluidValue> value)
        {
            _value = value;
        }

        public ArrayValue(FluidValue[] value)
        {
            _value = new List<FluidValue>(value);
        }

        public ArrayValue(IEnumerable<FluidValue> value)
        {
            _value = new List<FluidValue>();

            foreach (var item in value)
            {
                _value.Add(item);
            }
        }

        public override bool Equals(FluidValue other)
        {
            if (other.IsNil())
            {
                return _value.Count == 0;
            }

            if (other is ArrayValue arrayValue)
            {
                if (_value.Count != arrayValue._value.Count)
                {
                    return false;
                }

                for (var i = 0; i < _value.Count; i++)
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

        protected override FluidValue GetValue(string name, TemplateContext context)
        {
            switch (name)
            {
                case "size":
                    return NumberValue.Create(_value.Count);

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

        protected override FluidValue GetIndex(FluidValue index, TemplateContext context)
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

        public override void WriteTo(TextWriter writer, TextEncoder encoder, CultureInfo cultureInfo)
        {
            if (writer == null)
            {
                ExceptionHelper.ThrowArgumentNullException(nameof(writer));
            }

            if (encoder == null)
            {
                ExceptionHelper.ThrowArgumentNullException(nameof(encoder));
            }

            encoder.Encode(writer, ToStringValue());
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
            return _value.Contains(value);
        }

        public override IEnumerable<FluidValue> Enumerate()
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
