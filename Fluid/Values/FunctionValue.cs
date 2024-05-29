﻿using System.Globalization;
using System.Text.Encodings.Web;

namespace Fluid.Values
{
    public sealed class FunctionValue : FluidValue
    {
        public static readonly FunctionValue NoOp = new FunctionValue((_, _) => new ValueTask<FluidValue>(NilValue.Instance));
        private readonly Func<FunctionArguments, TemplateContext, ValueTask<FluidValue>> _action;

        public FunctionValue(Func<FunctionArguments, TemplateContext, ValueTask<FluidValue>> asyncAction)
        {
            _action = asyncAction;
        }

        public FunctionValue(Func<FunctionArguments, TemplateContext, FluidValue> action)
        {
            _action = (args, c) => new ValueTask<FluidValue>(action(args, c));
        }

        public override FluidValues Type => FluidValues.Function;

        public override ValueTask<FluidValue> InvokeAsync(FunctionArguments arguments, TemplateContext context)
        {
            return _action == null ? NilValue.Instance : _action(arguments, context);
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

        [Obsolete("WriteTo is obsolete, prefer the WriteToAsync method.")]
        public override void WriteTo(TextWriter writer, TextEncoder encoder, CultureInfo cultureInfo)
        {
            // A function value should be invoked and its result used instead.
            // Calling write to is equivalent to rendering {{ alert }} instead of {{ alert() }}
        }

        public override ValueTask WriteToAsync(TextWriter writer, TextEncoder encoder, CultureInfo cultureInfo)
        {
            return default;
        }

        public override bool Equals(object obj)
        {
            return object.ReferenceEquals(this, obj);
        }

        public override int GetHashCode()
        {
            return _action == null ? 0 : _action.GetHashCode();
        }
    }
}
