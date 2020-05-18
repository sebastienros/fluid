using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Encodings.Web;

namespace Fluid.Values
{
    public sealed class StringValue : FluidValue
    {
        private readonly string _value;

        public StringValue(string value)
        {
            _value = value;
        }

        public StringValue(string value, bool encode)
        {
            _value = value;
            Encode = encode;
        }

        /// <summary>
        /// Gets or sets whether the string is encoded (default) or not when rendered.
        /// </summary>
        public bool Encode { get; set; } = true;

        public override FluidValues Type => FluidValues.String;

        public override bool Equals(FluidValue other)
        {
            if (other.IsNil())
            {
                return _value.Length == 0;
            }

            return _value == other.ToStringValue();
        }

        protected override FluidValue GetIndex(FluidValue index, TemplateContext context)
        {
            var i = (int) index.ToNumberValue();

            if (i < _value.Length)
            {
                return Create(_value[i]);
            }

            return NilValue.Instance;
        }

        protected override FluidValue GetValue(string name, TemplateContext context)
        {
            if (name == "size")
            { 
                return NumberValue.Create(_value.Length);
            }

            return NilValue.Instance;
        }

        public override bool ToBooleanValue()
        {
            return true;
        }

        public override decimal ToNumberValue()
        {
            if (decimal.TryParse(_value, out var value))
            {
                return value;
            }

            return 0;
        }

        public override string ToStringValue()
        {
            return _value;
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

            if (String.IsNullOrEmpty(_value))
            {
                return;
            }

            if (Encode)
            {
                encoder.Encode(writer, _value);
            }
            else
            {
                writer.Write(_value);
            }
        }

        public override object ToObjectValue()
        {
            return _value;
        }

        public override bool Contains(FluidValue value)
        {
            return _value.Contains(value.ToStringValue());
        }

        public override IEnumerable<FluidValue> Enumerate()
        {
            foreach (var c in _value)
            {
                yield return new StringValue(c.ToString());
            }
        }

        public override bool Equals(object other)
        {
            // The is operator will return false if null
            if (other is StringValue otherValue)
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
