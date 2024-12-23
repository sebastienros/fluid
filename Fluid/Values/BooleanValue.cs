using Fluid.Utils;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;

namespace Fluid.Values
{
    public sealed class BooleanValue : FluidValue
    {
        public static readonly BooleanValue False = new BooleanValue(false);
        public static readonly BooleanValue True = new BooleanValue(true);

        private static readonly object BoxedTrue = true;
        private static readonly object BoxedFalse = false;

        private readonly bool _value;

        private BooleanValue(bool value)
        {
            _value = value;
        }

        public override FluidValues Type => FluidValues.Boolean;

        public static BooleanValue Create(bool value)
        {
            return value ? True : False;
        }

        public override bool Equals(FluidValue other)
        {
            // blank == false -> true
            if (other.Type == FluidValues.Blank) return !_value;

            return _value == other.ToBooleanValue();
        }

        public override bool ToBooleanValue()
        {
            return _value;
        }

        public override decimal ToNumberValue()
        {
            return _value ? 1 : 0;
        }

        public override string ToStringValue()
        {
            return _value ? "true" : "false";
        }

        [Obsolete("WriteTo is obsolete, prefer the WriteToAsync method.")]
        public override void WriteTo(TextWriter writer, TextEncoder encoder, CultureInfo cultureInfo)
        {
            AssertWriteToParameters(writer, encoder, cultureInfo);
            writer.Write(encoder.Encode(ToStringValue()));
        }

        public override ValueTask WriteToAsync(TextWriter writer, TextEncoder encoder, CultureInfo cultureInfo)
        {
            AssertWriteToParameters(writer, encoder, cultureInfo);
            var task = writer.WriteAsync(encoder.Encode(ToStringValue()));

            if (task.IsCompletedSuccessfully())
            {
                return default;
            }

            return Awaited(task);

            [MethodImpl(MethodImplOptions.NoInlining)]
            static async ValueTask Awaited(Task t)
            {
                await t;
                return;
            }
        }

        public override object ToObjectValue()
        {
            return _value ? BoxedTrue : BoxedFalse;
        }

        public override bool Equals(object obj)
        {
            // The is operator will return false if null
            if (obj is BooleanValue otherValue)
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
