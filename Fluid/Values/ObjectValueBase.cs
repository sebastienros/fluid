using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Fluid.Values
{
    /// <summary>
    /// Inherits from this class to implement custom object wrappers.
    /// </summary>
    public abstract class ObjectValueBase : FluidValue
    {
        protected static readonly char[] MemberSeparators = new [] { '.' };

        protected bool? _isModelType;

        public ObjectValueBase(object value)
        {
            Value = value;
        }

        public object Value { get; }

        public override FluidValues Type => FluidValues.Object;

        public override bool Equals(FluidValue other)
        {
            if (other.IsNil())
            {
                switch (Value)
                {
                    case ICollection collection:
                        return collection.Count == 0;

                    case IEnumerable enumerable:
                        return !enumerable.GetEnumerator().MoveNext();
                }

                return false;
            }

            return other is ObjectValueBase && ((ObjectValueBase)other).Value == Value;
        }

        public override ValueTask<FluidValue> GetValueAsync(string name, TemplateContext context)
        {
            // The model type has a custom ability to allow any of its members optionally
            _isModelType ??= context.Model != null && context.Model?.ToObjectValue()?.GetType() == Value.GetType();

            var accessor = context.Options.MemberAccessStrategy.GetAccessor(Value.GetType(), name);

            if (accessor == null && _isModelType.Value && context.AllowModelMembers)
            {
                accessor = MemberAccessStrategyExtensions.GetNamedAccessor(Value.GetType(), name, context.Options.MemberAccessStrategy.MemberNameStrategy);
            }

            if (name.IndexOf(".", StringComparison.OrdinalIgnoreCase) != -1)
            {
                // Try to access the property with dots inside
                if (accessor != null)
                {
                    if (accessor is IAsyncMemberAccessor asyncAccessor)
                    {
                        return Awaited(asyncAccessor, Value, name, context);
                    }

                    var directValue = accessor.Get(Value, name, context);

                    if (directValue != null)
                    {
                        return new ValueTask<FluidValue>(FluidValue.Create(directValue, context.Options));
                    }
                }

                // Otherwise split the name in different segments
                return GetNestedValueAsync(name, context);
            }
            else
            {
                if (accessor != null)
                {
                    if (accessor is IAsyncMemberAccessor asyncAccessor)
                    {
                        return Awaited(asyncAccessor, Value, name, context);
                    }

                    return FluidValue.Create(accessor.Get(Value, name, context), context.Options);
                }
            }

            return new ValueTask<FluidValue>(NilValue.Instance);


            static async ValueTask<FluidValue> Awaited(
                IAsyncMemberAccessor asyncAccessor,
                object value,
                string n,
                TemplateContext ctx)
            {
                return Create(await asyncAccessor.GetAsync(value, n, ctx), ctx.Options);
            }
        }

        private async ValueTask<FluidValue> GetNestedValueAsync(string name, TemplateContext context)
        {
            var members = name.Split(MemberSeparators);

            object target = Value;

            foreach (var prop in members)
            {
                if (target == null)
                {
                    return NilValue.Instance;
                }

                var accessor = context.Options.MemberAccessStrategy.GetAccessor(target.GetType(), prop);

                if (accessor == null)
                {
                    return NilValue.Instance;
                }

                if (accessor is IAsyncMemberAccessor asyncAccessor)
                {
                    target = await asyncAccessor.GetAsync(target, prop, context);
                }
                else
                {
                    target = accessor.Get(target, prop, context);
                }
            }

            return FluidValue.Create(target, context.Options);
        }

        public override ValueTask<FluidValue> GetIndexAsync(FluidValue index, TemplateContext context)
        {
            return GetValueAsync(index.ToStringValue(), context);
        }

        public override bool ToBooleanValue()
        {
            return Value != null;
        }

        public override decimal ToNumberValue()
        {
            return Convert.ToDecimal(Value);
        }

        public override void WriteTo(TextWriter writer, TextEncoder encoder, CultureInfo cultureInfo)
        {
            AssertWriteToParameters(writer, encoder, cultureInfo);
            writer.Write(encoder.Encode(ToStringValue()));
        }

        public override string ToStringValue()
        {
            return Convert.ToString(Value);
        }

        public override object ToObjectValue()
        {
            return Value;
        }

        public override bool Equals(object other)
        {
            // The is operator will return false if null
            if (other is ObjectValueBase otherValue)
            {
                return Value.Equals(otherValue.Value);
            }

            return false;
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }
    }
}
