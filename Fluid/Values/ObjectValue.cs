using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Fluid.Values
{
    public sealed class ObjectValue : FluidValue
    {
        private static readonly char[] MemberSeparators = new [] { '.' };

        private readonly object _value;

        public ObjectValue(object value)
        {
            _value = value;
        }

        public override FluidValues Type => FluidValues.Object;

        public override bool Equals(FluidValue other)
        {
            if (other.IsNil())
            {
                switch (_value)
                {
                    case ICollection collection:
                        return collection.Count == 0;

                    case IEnumerable enumerable:
                        return !enumerable.GetEnumerator().MoveNext();
                }

                return false;
            }

            return other is ObjectValue && ((ObjectValue)other)._value == _value;
        }

        public override ValueTask<FluidValue> GetValueAsync(string name, TemplateContext context)
        {
            static async ValueTask<FluidValue> Awaited(
                IAsyncMemberAccessor asyncAccessor,
                object value,
                string n,
                TemplateContext ctx)
            {
                return Create(await asyncAccessor.GetAsync(value, n, ctx));
            }

            if (name.Contains("."))
            {
                var accessor = context.MemberAccessStrategy.GetAccessor(_value.GetType(), name);

                // Try to access the property with dots inside
                if (accessor != null)
                {
                    if (accessor is IAsyncMemberAccessor asyncAccessor)
                    {
                        return Awaited(asyncAccessor, _value, name, context);
                    }

                    var directValue = accessor.Get(_value, name, context);

                    if (directValue != null)
                    {
                        return new ValueTask<FluidValue>(FluidValue.Create(directValue));
                    }
                }

                // Otherwise split the name in different segments
                return GetNestedValueAsync(name, context);
            }
            else
            {
                var accessor = context.MemberAccessStrategy.GetAccessor(_value.GetType(), name);

                if (accessor != null)
                {
                    if (accessor is IAsyncMemberAccessor asyncAccessor)
                    {
                        return Awaited(asyncAccessor, _value, name, context);
                    }

                    return new ValueTask<FluidValue>(FluidValue.Create(accessor.Get(_value, name, context)));
                }
            }

            return new ValueTask<FluidValue>(NilValue.Instance);
        }

        private async ValueTask<FluidValue> GetNestedValueAsync(string name, TemplateContext context)
        {
            var members = name.Split(MemberSeparators);

            object target = _value;

            foreach (var prop in members)
            {
                if (target == null)
                {
                    return NilValue.Instance;
                }

                var accessor = context.MemberAccessStrategy.GetAccessor(target.GetType(), prop);

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

            return FluidValue.Create(target);
        }

        public override ValueTask<FluidValue> GetIndexAsync(FluidValue index, TemplateContext context)
        {
            return GetValueAsync(index.ToStringValue(), context);
        }

        public override bool ToBooleanValue()
        {
            return _value != null;
        }

        public override double ToNumberValue()
        {
            return Convert.ToDouble(_value);
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

            encoder.Encode(writer, _value.ToString());
        }

        public override string ToStringValue()
        {
            return Convert.ToString(_value);
        }

        public override object ToObjectValue()
        {
            return _value;
        }

        public override bool Equals(object other)
        {
            // The is operator will return false if null
            if (other is ObjectValue otherValue)
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
