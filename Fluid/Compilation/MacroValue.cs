using Fluid.Values;
using System.Globalization;
using System.Text.Encodings.Web;

namespace Fluid.Compilation
{
    /// <summary>
    /// Like <see cref="FunctionValue"/> but with a <see cref="TextEncoder"/> instance to render macros.
    /// </summary>
    public sealed class MacroValue : FluidValue
    {
        private readonly Func<FunctionArguments, TemplateContext, TextEncoder, ValueTask<FluidValue>> _action;
        private readonly TextEncoder _encoder;

        public MacroValue(Func<FunctionArguments, TemplateContext, TextEncoder, ValueTask<FluidValue>> asyncAction, TextEncoder encoder)
        {
            _action = asyncAction;
            _encoder = encoder;
        }

        public override FluidValues Type => FluidValues.Object;

        public override ValueTask<FluidValue> InvokeAsync(FunctionArguments arguments, TemplateContext context)
        {
            return _action == null ? NilValue.Instance : _action(arguments, context, _encoder);
        }

        public override bool Equals(FluidValue other)
        {
            return false;
        }

        public override bool ToBooleanValue()
        {
            return true;
        }

        public override decimal ToNumberValue()
        {
            return 0;
        }

        public override object ToObjectValue()
        {
            return "";
        }

        public override string ToStringValue()
        {
            return "";
        }

        public override bool IsNil()
        {
            return false;
        }

        public override void WriteTo(TextWriter writer, TextEncoder encoder, CultureInfo cultureInfo)
        {
            // A function value should be invoked and its result used instead.
            // Calling write to is equivalent to rendering {{ alert }} instead of {{ alert() }}
        }

        public override bool Equals(object other)
        {
            return object.ReferenceEquals(this, other);
        }

        public override int GetHashCode()
        {
            return _action == null ? 0 : _action.GetHashCode();
        }
    }
}
