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

        internal ArrayValue(List<FluidValue> value)
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
                        return FluidValue.Create(_value[0]);
                    }
                    break;

                case "last":
                    if (_value.Length > 0)
                    {
                        return FluidValue.Create(_value[_value.Length - 1]);
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
                return FluidValue.Create(_value[i]);
            }

            return NilValue.Instance;
        }

        public override bool ToBooleanValue()
        {
            return true;
        }

        public override decimal ToNumberValue()
        {
            return 0;
        }

        public FluidValue[] Values => _value;
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
            return Array.IndexOf(_value, value) > -1;
        }

        public override IEnumerable<FluidValue> Enumerate()
        {
            return _value;
        }

        internal override string[] ToStringArray()
        {
            var array = new string[_value.Length];
            for (var i = 0; i < _value.Length; i++)
            {
                array[i] = _value[i].ToStringValue();
            }

            return array;
        }

        internal override List<FluidValue> ToList()
        {
            return new(_value);
        }

        internal override FluidValue FirstOrDefault()
        {
            return _value.Length > 0 ? _value[0] : null;
        }

        internal override FluidValue LastOrDefault()
        {
            return _value.Length > 0 ? _value[_value.Length - 1] : null;
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
