using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Fluid.Values
{
    public sealed class ArrayValue : FluidValue
    {
        public static readonly ArrayValue Empty = new ArrayValue(Array.Empty<FluidValue>());

        private readonly IReadOnlyList<FluidValue> _value;

        public override FluidValues Type => FluidValues.Array;

        public IReadOnlyList<FluidValue> Values => _value;

        private ArrayValue(IReadOnlyList<FluidValue> value)
        {
            _value = value;
        }

        public static ArrayValue Create(FluidValue[] value)
        {
            return new ArrayValue(value);
        }

        public static ArrayValue Create(IEnumerable<FluidValue> value)
        {
            return new ArrayValue(value.ToArray());
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
            else if (other.Type == FluidValues.Empty)
            {
                return _value.Count == 0;
            }

            return false;
        }

        public override ValueTask<FluidValue> GetValueAsync(string name, TemplateContext context)
        {
            switch (name)
            {
                case "size":
                    return NumberValue.Create(_value.Count);

                case "first":
                    if (_value.Count > 0)
                    {
                        return _value[0];
                    }
                    break;

                case "last":
                    if (_value.Count > 0)
                    {
                        return _value[_value.Count - 1];
                    }
                    break;

            }

            return NilValue.Instance;
        }

        public override ValueTask<FluidValue> GetIndexAsync(FluidValue index, TemplateContext context)
        {
            var i = (int)index.ToNumberValue();

            if (i >= 0 && i < _value.Count)
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
            return _value.Count;
        }

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
            return _value.Contains(value);
        }

        public override ValueTask<IEnumerable<FluidValue>> EnumerateAsync(TemplateContext context)
        {
            return new ValueTask<IEnumerable<FluidValue>>(_value);
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
