﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using Parlot;

namespace Fluid.Values
{
    public sealed class StringValue : FluidValue, IEquatable<StringValue>
    {
        public static readonly StringValue Empty = new StringValue("");

        private static readonly StringValue[] CharToString = new StringValue[256];

        private readonly string _value;

        static StringValue()
        {
            for (var i = 0; i < CharToString.Length; ++i)
            {
                var c = (char) i;
                CharToString[i] = new StringValue(c.ToString());
            }
        }

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static StringValue Create(char c)
        {
            var temp = CharToString;
            if ((uint) c < (uint) temp.Length)
            {
                return temp[c];
            }
            return new StringValue(c.ToString());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static StringValue Create(string s)
        {
            if (s == "")
            {
                return Empty;
            }

            return s.Length == 1
                ? Create(s[0])
                : new StringValue(s);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static StringValue Create(in TextSpan span)
        {
            if (span.Length == 0)
            {
                return Empty;
            }

            return span.Length == 1
                ? Create(span.Buffer[span.Offset])
                : new StringValue(span.ToString());
        }

        public override bool Equals(FluidValue other)
        {
            if (other.Type == FluidValues.String) return _value == other.ToStringValue();
            if (other == BlankValue.Instance) return String.IsNullOrWhiteSpace(ToStringValue());
            if (other == EmptyValue.Instance) return ToStringValue().Length == 0;

            return false;
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

        public override IEnumerable<FluidValue> Enumerate()
        {
            foreach (var c in _value)
            {
                yield return StringValue.Create(c);
            }
        }

        internal override string[] ToStringArray()
        {
            var array = new string[_value.Length];
            for (var i = 0; i < _value.Length; i++)
            {
                array[i] = _value[i].ToString();
            }

            return array;
        }

        internal override List<FluidValue> ToList()
        {
            var list = new List<FluidValue>(_value.Length);
            foreach (var c in _value)
            {
                list.Add(Create(c));
            }

            return list;
        }

        internal override FluidValue FirstOrDefault()
        {
            return _value.Length > 0 ? Create(_value[0]) : null;
        }

        internal override FluidValue LastOrDefault()
        {
            return _value.Length > 0 ? Create(_value[_value.Length - 1]) : null;
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
