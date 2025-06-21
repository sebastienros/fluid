using Fluid.Utils;
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
        public static readonly NumberValue Zero = new NumberValue(0M);

        private static readonly NumberValue[] IntToString = new NumberValue[1024];

        private readonly decimal _value;

        static NumberValue()
        {
            for (var i = 0; i < IntToString.Length; ++i)
            {
                IntToString[i] = new NumberValue(i);
            }
        }

        private NumberValue(decimal value)
        {
            _value = value;
        }

        public override FluidValues Type => FluidValues.Number;

        public override bool Equals(FluidValue other)
        {
            // Delegating other types 
            if (other == BlankValue.Instance || other == NilValue.Instance || other == EmptyValue.Instance)
            {
                return false;
            }

            return _value == other.ToNumberValue();
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
            return _value.ToString(CultureInfo.InvariantCulture);
        }

        [Obsolete("WriteTo is obsolete, prefer the WriteToAsync method.")]
        public override void WriteTo(TextWriter writer, TextEncoder encoder, CultureInfo cultureInfo)
        {
            AssertWriteToParameters(writer, encoder, cultureInfo);
            writer.Write(encoder.Encode(_value.ToString(cultureInfo)));
        }

        public override ValueTask WriteToAsync(TextWriter writer, TextEncoder encoder, CultureInfo cultureInfo)
        {
            AssertWriteToParameters(writer, encoder, cultureInfo);

            Task task = default;

            var scale = GetScale(_value);
            if (scale == 0)
            {
                // If the scale is zero, we can write the value directly without formatting
                task = writer.WriteAsync(encoder.Encode(_value.ToString(cultureInfo)));
            }
            else if (_value * (10 * scale) % (10 * scale) == 0)
            {
                // If the decimal part is zero(s), write one only
                task = writer.WriteAsync(encoder.Encode(_value.ToString("F1", cultureInfo)));
            }
            else
            {
                // For larger scales, we use G29 to avoid trailing zeros
                task = writer.WriteAsync(encoder.Encode(_value.ToString("G29", cultureInfo)));
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
        /// If the value is zero, the scale is zero.
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
