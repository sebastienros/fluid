using Fluid.Utils;
using Parlot;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;

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
                var c = (char)i;
                CharToString[i] = new StringValue(c.ToString());
            }
        }

        public StringValue(string value)
        {
            // Returns a StringValue instance and not NilValue since this is what is asked for.
            // However FluidValue.Create(null) returns NilValue.

            _value = value ?? NilValue.Instance.ToStringValue();
        }

        public StringValue(string value, bool encode) : this(value)
        {
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
            return new StringValue(c.ToString());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static StringValue Create(string s)
        {
            if (String.IsNullOrEmpty(s))
            {
                return Empty;
            }

            return s.Length == 1
                ? Create(s[0])
                : new StringValue(s);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static StringValue Create(string s, bool encode)
        {
            return new StringValue(s, encode);
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
            if (other.Type == FluidValues.String)
            {
                return _value == other.ToStringValue();
            }

            // Delegating other types
            if (other == BlankValue.Instance || other == NilValue.Instance || other == EmptyValue.Instance)
            {
                return other.Equals(this);
            }

            return false;
        }

        public override ValueTask<FluidValue> GetIndexAsync(FluidValue index, TemplateContext context)
        {
            // Indexer on string values should return nil.
            return NilValue.Instance;
        }

        public override ValueTask<FluidValue> GetValueAsync(string name, TemplateContext context)
        {
            return name switch
            {
                "size" => NumberValue.Create(_value.Length),
                "first" => _value.Length > 0 ? Create(_value[0]) : NilValue.Instance,
                "last" => _value.Length > 0 ? Create(_value[^1]) : NilValue.Instance,
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

        public override ValueTask WriteToAsync(TextWriter writer, TextEncoder encoder, CultureInfo cultureInfo)
        {
            AssertWriteToParameters(writer, encoder, cultureInfo);
            if (string.IsNullOrEmpty(_value))
            {
                return default;
            }

            Task task;

            if (Encode)
            {
                // perf: Don't use this overload
                // encoder.Encode(writer, _value);

                // Use a transient string instead of calling
                // encoder.Encode(TextWriter) since it would
                // call writer.Write on each char if the string
                // has even a single char to encode
                task = writer.WriteAsync(encoder.Encode(_value));
            }
            else
            {
                task = writer.WriteAsync(_value);
            }

            if (task.IsCompletedSuccessfully())
            {
                return default;
            }

            return Awaited(task);

            static async ValueTask Awaited(Task t)
            {
                await t;
                return;
            }
        }

        public override object ToObjectValue()
        {
            return _value;
        }

        public override ValueTask<bool> ContainsAsync(FluidValue value, TemplateContext context)
        {
            return new ValueTask<bool>(_value.Contains(value.ToStringValue(context)));
        }

        public override async IAsyncEnumerable<FluidValue> EnumerateAsync(TemplateContext context)
        {
            yield return this;
            await Task.CompletedTask;
        }

        public override bool Equals(object obj)
        {
            return obj is StringValue s && Equals(s);
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
