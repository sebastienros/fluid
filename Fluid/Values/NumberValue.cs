using Fluid.Utils;
using System.Buffers;
using System.Globalization;
using System.Text.Encodings.Web;

namespace Fluid.Values
{
    /// Numbers are stored as decimal values to handle the best possible precision.
    /// Decimals also have the capacity of retaining their precision across
    /// operations:
    /// 1 * 2 = 2
    /// 1.0 * 2.0 = 2.00
    public sealed class NumberValue : FluidValue, IEquatable<NumberValue>
    {
        /// <summary>
        /// The largest value (exclusive) kept as a shared instance with precomputed text.
        /// </summary>
        private const int InternedLimit = 1024;

        public static readonly NumberValue Zero;

        private static readonly NumberValue[] IntToString = new NumberValue[InternedLimit];

        private readonly decimal _value;

        /// <summary>
        /// The rendered form of the value, when it is known to be culture-independent. Non-negative
        /// integers below <see cref="IntToString"/>'s length format as plain ASCII digits in every
        /// culture, so their text can be precomputed and written without formatting the decimal.
        /// </summary>
        private readonly string _text;

        static NumberValue()
        {
            for (var i = 0; i < IntToString.Length; ++i)
            {
                IntToString[i] = new NumberValue(i, i.ToString(CultureInfo.InvariantCulture));
            }

            // Share the instance Create(0) hands out, so there is only ever one canonical zero.
            Zero = IntToString[0];
        }

        private NumberValue(decimal value)
        {
            _value = value;
        }

        private NumberValue(int value, string text)
        {
            _value = value;
            _text = text;
        }

        public override FluidValues Type => FluidValues.Number;

        public override bool Equals(FluidValue other)
        {
            // Delegating special cases to other types
            if (other == BlankValue.Instance || other == NilValue.Instance || other == EmptyValue.Instance)
            {
                return false;
            }

            if (other.Type != FluidValues.Number)
            {
                return false;
            }

            return _value == other.ToNumberValue();
        }

        public override ValueTask<FluidValue> GetIndexAsync(FluidValue index, TemplateContext context)
        {
            // Integer bit access (e.g. 2[1] == 1). Non-integer numeric values don't support indexers.
            if (GetScale(_value) != 0)
            {
                return NilValue.Instance;
            }

            if (index.Type != FluidValues.Number || GetScale(index.ToNumberValue(context)) != 0)
            {
                throw new LiquidException($"cannot select the property '{index.ToStringValue(context)}'");
            }

            var bitIndexAsDecimal = index.ToNumberValue(context);

            if (bitIndexAsDecimal < 0 || bitIndexAsDecimal > int.MaxValue)
            {
                return Zero;
            }

            var bitIndex = (int)bitIndexAsDecimal;

            long number;
            try
            {
                number = decimal.ToInt64(_value);
            }
            catch (OverflowException)
            {
                return Zero;
            }

            if (bitIndex >= 63)
            {
                return number < 0 ? Create(1) : Zero;
            }

            return Create((number >> bitIndex) & 1L);
        }

        public override bool ToBooleanValue()
        {
            return true;
        }

        public override decimal ToNumberValue()
        {
            return _value;
        }

        public override string ToStringValue()
        {
            return _text ?? _value.ToString(CultureInfo.InvariantCulture);
        }

        public override ValueTask WriteToAsync(IFluidOutput output, TextEncoder encoder, CultureInfo cultureInfo)
        {
            AssertWriteToParameters(output, encoder, cultureInfo);

            if (_text is not null)
            {
                output.Write(encoder, _text);
                return default;
            }

            var scale = GetScale(_value);

            #if NET8_0_OR_GREATER
            ReadOnlySpan<char> format = default;

            if (scale == 0)
            {
                // Default format.
            }
            else if (_value * (10 * scale) % (10 * scale) == 0)
            {
                // If the decimal part is zero(s), write one only
                format = "F1";
            }
            else
            {
                // For larger scales, we use G29 to avoid trailing zeros
                format = "G29";
            }

            Span<char> scratch = stackalloc char[64];
            if (_value.TryFormat(scratch, out var written, format, cultureInfo))
            {
                output.Write(encoder, scratch.Slice(0, written));
                return default;
            }

            // Extremely defensive fallback (very unlikely for decimal): rent a larger buffer.
            // Keep allocation-free in the common case.
            var pool = ArrayPool<char>.Shared;
            var rented = pool.Rent(256);
            try
            {
                var span = rented.AsSpan();
                if (_value.TryFormat(span, out written, format, cultureInfo))
                {
                    output.Write(encoder, span.Slice(0, written));
                    return default;
                }

                // Last resort: string formatting.
                if (format.IsEmpty)
                {
                    output.Write(encoder, _value.ToString(cultureInfo));
                }
                else
                {
                    output.Write(encoder, _value.ToString(format.ToString(), cultureInfo));
                }
            }
            finally
            {
                pool.Return(rented);
            }
            #else
            if (scale == 0)
            {
                // If the scale is zero, we can write the value directly without formatting
                output.Write(encoder, _value.ToString(cultureInfo));
            }
            else if (_value * (10 * scale) % (10 * scale) == 0)
            {
                // If the decimal part is zero(s), write one only
                output.Write(encoder, _value.ToString("F1", cultureInfo));
            }
            else
            {
                // For larger scales, we use G29 to avoid trailing zeros
                output.Write(encoder, _value.ToString("G29", cultureInfo));
            }
            #endif

            return default;
        }

        public override IEnumerable<FluidValue> Enumerate(TemplateContext context)
        {
            return [this];
        }

        public override object ToObjectValue()
        {
            return _value;
        }

        public override bool Equals(object obj)
        {
            return obj is NumberValue n && Equals(n);
        }

        public bool Equals(NumberValue other)
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

        public static NumberValue Create(string value)
        {
            if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
            {
                return Create(d);
            }

            return Zero;
        }

        public static NumberValue Create(decimal value)
        {
            // Reuse the interned instances for small non-negative whole numbers. That is by far the most
            // common shape of number flowing through a template (loop counters, sizes, quantities, prices)
            // and lets them render from precomputed text instead of formatting a decimal.
            // The scale is part of a number's identity in Liquid (1.0 * 2.0 == 2.00), so only scale 0 qualifies.
            if (GetScale(value) == 0 && value >= 0m && value < InternedLimit)
            {
                return IntToString[(int)value];
            }

            return new NumberValue(value);
        }

        internal static NumberValue Create(uint value)
        {
            var temp = IntToString;
            if (value < (uint)temp.Length)
            {
                return temp[value];
            }
            return new NumberValue(value);
        }

        internal static NumberValue Create(int value)
        {
            var temp = IntToString;
            if (value >= 0 && value < temp.Length)
            {
                return temp[value];
            }
            return new NumberValue(value);
        }

        /// <summary>
        /// Gets the scale of a decimal value, which is the number of digits to the right of the decimal point.
        /// </summary>
        public static byte GetScale(decimal value)
        {
#if NET8_0_OR_GREATER
            return value.Scale;
#else       
            return unchecked((byte)(decimal.GetBits(value)[3] >> 16));
#endif
        }
    }
}
