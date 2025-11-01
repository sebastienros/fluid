﻿using System.Collections;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using Fluid.Utils;

namespace Fluid.Values
{
    /// <summary>
    /// Inherits from this class to implement custom object wrappers.
    /// </summary>
    public abstract class ObjectValueBase : FluidValue
    {
        protected static readonly char[] MemberSeparators = ['.'];

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
                return Value switch
                {
                    ICollection collection => collection.Count == 0,
                    IEnumerable enumerable => !enumerable.GetEnumerator().MoveNext(),
                    _ => false,
                };

            }

            return other is ObjectValueBase otherObject && Value.Equals(otherObject.Value);
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

            if (name.Contains('.'))
            {
                if (accessor != null)
                {
                    if (accessor is IAsyncMemberAccessor asyncAccessor)
                    {
                        return Awaited(asyncAccessor, Value, name, context);
                    }

                    var directValue = accessor.Get(Value, name, context);

                    if (directValue != null)
                    {
                        return new ValueTask<FluidValue>(Create(directValue, context.Options));
                    }
                }

                return GetNestedValueAsync(name, context);
            }

            if (accessor != null)
            {
                if (accessor is IAsyncMemberAccessor asyncAccessor)
                {
                    return Awaited(asyncAccessor, Value, name, context);
                }

                return Create(accessor.Get(Value, name, context), context.Options);
            }

            if (context.Undefined is not null)
            {
                return context.Undefined.Invoke(name);
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
            var target = Value;
            List<string> segments = context.Undefined is not null ? [] : null;

            foreach (var prop in members)
            {
                if (context.Undefined is not null)
                {
                    segments.Add(prop);
                }

                if (target == null)
                {
                    return NilValue.Instance;
                }

                var accessor = context.Options.MemberAccessStrategy.GetAccessor(target.GetType(), prop);

                if (accessor == null)
                {
                    if (context.Undefined is not null)
                    {
                        return await context.Undefined.Invoke(string.Join(".", segments));
                    }
                    return UndefinedValue.Instance;
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

            return Create(target, context.Options);
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

        [Obsolete("WriteTo is obsolete, prefer the WriteToAsync method.")]
        public override void WriteTo(TextWriter writer, TextEncoder encoder, CultureInfo cultureInfo)
        {
            AssertWriteToParameters(writer, encoder, cultureInfo);

            var value = ToStringValue();
            
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            writer.Write(encoder.Encode(value));
        }

        public override ValueTask WriteToAsync(TextWriter writer, TextEncoder encoder, CultureInfo cultureInfo)
        {
            AssertWriteToParameters(writer, encoder, cultureInfo);

            var value = ToStringValue();

            if (string.IsNullOrEmpty(value))
            {
                return default;
            }

            var task = writer.WriteAsync(encoder.Encode(value));

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

        public override string ToStringValue()
        {
            return Convert.ToString(Value);
        }

        public override object ToObjectValue()
        {
            return Value;
        }

        public override bool Equals(object obj)
        {
            // The is operator will return false if null
            if (obj is ObjectValueBase otherValue)
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
