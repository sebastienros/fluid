using System.Collections;
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

        /// <summary>
        /// A resolved <see cref="IMemberAccessor"/> remembered by a single call site, so that repeatedly
        /// reading the same member off the same type (a loop body, typically) doesn't hash the member
        /// name into the strategy's dictionary on every iteration.
        /// </summary>
        /// <remarks>
        /// Immutable, and published by a single reference assignment. Under the .NET runtime memory model
        /// that assignment is a release store, so a thread that reads the entry either sees the previous
        /// one or this one fully initialized -- never a half-built entry.
        /// </remarks>
        internal sealed class AccessorCacheEntry
        {
            /// <summary>
            /// Marks a call site that has seen too many distinct types to be worth caching, such as a
            /// loop over a heterogeneous collection. Without it such a site would miss every time and
            /// allocate a replacement entry per access, which is worse than not caching at all.
            /// </summary>
            public static readonly AccessorCacheEntry Disabled = new(null, null, null, null, 0);

            public AccessorCacheEntry(object token, Type type, StringComparer comparer, IMemberAccessor accessor, int shapeMisses)
            {
                Token = token;
                Type = type;
                Comparer = comparer;
                Accessor = accessor;
                ShapeMisses = shapeMisses;
            }

            public readonly object Token;
            public readonly Type Type;
            public readonly StringComparer Comparer;
            public readonly IMemberAccessor Accessor;

            /// <summary>
            /// How often this site resolved against a different type or comparer. Counted on the entry
            /// rather than the segment so that tracking it costs no extra field per parsed segment.
            /// </summary>
            public readonly int ShapeMisses;
        }

        /// <summary>
        /// How many times a call site may change shape before it stops caching. A monomorphic site
        /// misses once when cold, so this only has to stay clear of the handful of misses that
        /// registering accessors for new types causes while an application warms up.
        /// </summary>
        private const int MaxShapeMisses = 4;

        public override ValueTask<FluidValue> GetValueAsync(string name, TemplateContext context)
        {
            var accessor = context.Options.MemberAccessStrategy.GetAccessor(Value.GetType(), name, context.Options.ModelNamesComparer);
            return GetValueAsync(name, context, name.Contains('.'), accessor);
        }

        internal ValueTask<FluidValue> GetValueAsync(string name, TemplateContext context, bool nameHasDot, ref AccessorCacheEntry cache)
        {
            return GetValueAsync(name, context, nameHasDot, GetAccessorCached(name, context, ref cache));
        }

        private IMemberAccessor GetAccessorCached(string name, TemplateContext context, ref AccessorCacheEntry cache)
        {
            var type = Value.GetType();
            var strategy = context.Options.MemberAccessStrategy;
            var comparer = context.Options.ModelNamesComparer;

            // A single read of the field; the entry is immutable once published.
            var entry = cache;

            if (ReferenceEquals(entry, AccessorCacheEntry.Disabled))
            {
                return strategy.GetAccessor(type, name, comparer);
            }

            // Read the token before resolving. A registration racing with this lookup then leaves the
            // entry stamped with the superseded token, so it is re-resolved on the next access instead
            // of being baked in.
            var token = strategy.AccessorCacheToken;

            if (entry is not null
                && ReferenceEquals(entry.Token, token)
                && ReferenceEquals(entry.Type, type)
                && ReferenceEquals(entry.Comparer, comparer))
            {
                return entry.Accessor;
            }

            var accessor = strategy.GetAccessor(type, name, comparer);

            // A null token means the strategy doesn't support caching.
            if (token is null)
            {
                return accessor;
            }

            // Only a change of shape counts towards the budget. Missing because the strategy registered
            // an accessor elsewhere says nothing about how many types this site sees, and would
            // otherwise disable caching everywhere during warm-up.
            var shapeMisses = entry is null || (ReferenceEquals(entry.Type, type) && ReferenceEquals(entry.Comparer, comparer))
                ? entry?.ShapeMisses ?? 0
                : entry.ShapeMisses + 1;

            cache = shapeMisses > MaxShapeMisses
                ? AccessorCacheEntry.Disabled
                : new AccessorCacheEntry(token, type, comparer, accessor, shapeMisses);

            return accessor;
        }

        private ValueTask<FluidValue> GetValueAsync(string name, TemplateContext context, bool nameHasDot, IMemberAccessor accessor)
        {
            if (nameHasDot)
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
                        return FluidValue.Create(directValue, context.Options);
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

            if (context.Options.StrictVariables)
            {
                throw new FluidException($"Undefined variable '{name}'");
            }
            if (context.Undefined is not null)
            {
                return context.Undefined.Invoke(name, Value.GetType());
            }
            return NilValue.Instance;


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

                var accessor = context.Options.MemberAccessStrategy.GetAccessor(target.GetType(), prop, context.Options.ModelNamesComparer);

                if (accessor == null)
                {
                    if (context.Options.StrictVariables)
                    {
                        throw new FluidException($"Undefined variable '{string.Join(".", segments)}'");
                    }
                    if (context.Undefined is not null)
                    {
                        return await context.Undefined.Invoke(string.Join(".", segments), target.GetType());
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
            try
            {
                return Convert.ToDecimal(Value);
            }
            catch
            {
                return 0;
            }
        }

        public override ValueTask WriteToAsync(IFluidOutput output, TextEncoder encoder, CultureInfo cultureInfo)
        {
            AssertWriteToParameters(output, encoder, cultureInfo);

            var value = ToStringValue();

            if (string.IsNullOrEmpty(value))
            {
                return default;
            }

            output.Write(encoder, value);
            return default;
        }

        public override IEnumerable<FluidValue> Enumerate(TemplateContext context)
        {
            return [this];
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
