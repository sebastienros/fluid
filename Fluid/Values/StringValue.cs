using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Fluid.Values
{
    public sealed class StringValue : FluidValue, IEquatable<StringValue>
    {
        public static readonly StringValue Empty = new StringValue("", false);

        private static readonly StringValue[] CharToString = new StringValue[256];

        private readonly string _value;

        static StringValue()
        {
            for (var i = 0; i < CharToString.Length; ++i)
            {
                var c = (char)i;
                CharToString[i] = new StringValue(c.ToString(), false);
            }
        }

        private StringValue(string value, bool encode)
        {
            // Returns a StringValue instance and not NilValue since this is what is asked for.
            // However FluidValue.Create(null) returns NilValue.

            _value = value ?? NilValue.Instance.ToStringValue();
            Encode = encode;
        }

        /// <summary>
        /// Gets or sets whether the string is encoded (default) or not when rendered.
        /// </summary>
        public bool Encode { get; set; } = true;

        public override FluidValues Type => FluidValues.String;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static StringValue Create(char c)
        {
            var temp = CharToString;
            if ((uint)c < (uint)temp.Length)
            {
                return temp[c];
            }
            return new StringValue(c.ToString(), false);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static StringValue Create(string s, bool encode)
        {
            if (String.IsNullOrEmpty(s))
            {
                return Empty;
            }

            return s.Length == 1
                ? Create(s[0])
                : new StringValue(s, encode);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static StringValue Create(string s)
        {
            return Create(s, true);
        }

        public override bool Equals(FluidValue other)
        {
            if (other.Type == FluidValues.String) return _value == other.ToStringValue();

            // Delegating other types 
            if (other == BlankValue.Instance || other == NilValue.Instance || other == EmptyValue.Instance)
            {
                return other.Equals(this);
            }

            return false;
        }

        public override ValueTask<FluidValue> GetIndexAsync(FluidValue index, TemplateContext context)
        {
            var i = (int)index.ToNumberValue();

            if (i < _value.Length)
            {
                return Create(_value[i]);
            }

            return NilValue.Instance;
        }

        public override ValueTask<FluidValue> GetValueAsync(string name, TemplateContext context)
        {
            return name switch
            {
                "size" => NumberValue.Create(_value.Length),
                "first" => _value.Length > 0 ? Create(_value[0]) : NilValue.Instance,
                "last" => _value.Length > 0 ? Create(_value[_value.Length - 1]) : NilValue.Instance,
                _ => NilValue.Instance,
            };
        }

        public override bool ToBooleanValue()
        {
            return true;
        }

        public override decimal ToNumberValue()
        {
            if (_value == "")
            {
                return 0;
            }

            if (decimal.TryParse(_value, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
            {
                return d;
            }

            return 0;
        }

        public override string ToStringValue()
        {
            return _value;
        }

        public override void WriteTo(TextWriter writer, TextEncoder encoder, CultureInfo cultureInfo)
        {
            AssertWriteToParameters(writer, encoder, cultureInfo);
            if (string.IsNullOrEmpty(_value))
            {
                return;
            }

            if (Encode)
            {
                // perf: Don't use this overload
                // encoder.Encode(writer, _value);

                // Use a transient string instead of calling
                // encoder.Encode(TextWriter) since it would
                // call writer.Write on each char if the string
                // has even a single char to encode
                writer.Write(encoder.Encode(_value));
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

        public override ValueTask<IEnumerable<FluidValue>> EnumerateAsync(TemplateContext context)
        {
            return new ValueTask<IEnumerable<FluidValue>>(new[] { this });
        }

        public override bool Equals(object other)
        {
            return other is StringValue s && Equals(s);
        }

        public bool Equals(StringValue other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return _value == other._value;
        }

        public override int GetHashCode()
        {
            return _value.GetHashCode();
        }
    }
}
