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
        /// A resolved <see cref="MemberAccessor"/> remembered by a single call site, so that repeatedly
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
            /// Marks a call site that misses too often to be worth caching -- a loop over a
            /// heterogeneous collection, or a template rendered against several sets of options.
            /// Without it such a site would allocate a replacement entry on every access, which is
            /// worse than not caching at all.
            /// </summary>
            public static readonly AccessorCacheEntry Disabled = new(null, null, null, null, 0);

            public AccessorCacheEntry(object token, Type type, StringComparer comparer, MemberAccessor accessor, int misses)
            {
                Token = token;
                Type = type;
                Comparer = comparer;
                Accessor = accessor;
                Misses = misses;
            }

            public readonly object Token;
            public readonly Type Type;
            public readonly StringComparer Comparer;
            public readonly MemberAccessor Accessor;

            /// <summary>
            /// How often this site had to re-resolve. Counted on the entry rather than the segment so
            /// that tracking it costs no extra field per parsed segment.
            /// </summary>
            public readonly int Misses;
        }

        /// <summary>
        /// How many times a call site may re-resolve before it stops caching. A site that settles misses
        /// only while warming up, so this only has to clear the handful of misses that resolving the
        /// first accessors for a model type costs.
        /// </summary>
        private const int MaxMisses = 4;

        public override ValueTask<FluidValue> GetValueAsync(string name, TemplateContext context)
        {
            var accessor = context.Options.MemberAccessStrategy.GetAccessor(Value.GetType(), name, context.Options.ModelNamesComparer);
            return GetValueAsync(name, context, name.Contains('.'), accessor);
        }

        internal ValueTask<FluidValue> GetValueAsync(string name, TemplateContext context, bool nameHasDot, ref AccessorCacheEntry cache)
        {
            return GetValueAsync(name, context, nameHasDot, GetAccessorCached(name, context, ref cache));
        }

        private MemberAccessor GetAccessorCached(string name, TemplateContext context, ref AccessorCacheEntry cache)
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

            // Every miss counts, whatever the cause. A site that keeps missing is one whose entry never
            // pays for itself, and it would otherwise allocate a replacement on every single access --
            // which is what a template rendered alternately against two TemplateOptions does, since the
            // type and comparer match every time and only the token differs.
            var misses = (entry?.Misses ?? 0) + 1;

            cache = misses > MaxMisses
                ? AccessorCacheEntry.Disabled
                : new AccessorCacheEntry(token, type, comparer, accessor, misses);

            return accessor;
        }

        private ValueTask<FluidValue> GetValueAsync(string name, TemplateContext context, bool nameHasDot, MemberAccessor accessor)
        {
            if (nameHasDot)
            {
                if (accessor != null)
                {
                    var task = accessor.GetAsync(Value, name, context);

                    if (!task.IsCompletedSuccessfully)
                    {
                        return AwaitedDirect(task, name, context);
                    }

                    var directValue = task.Result;

                    if (directValue != null)
                    {
                        return new ValueTask<FluidValue>(directValue);
                    }
                }

                return GetNestedValueAsync(name, context);
            }

            if (accessor != null)
            {
                var task = accessor.GetAsync(Value, name, context);
                if (task.IsCompletedSuccessfully)
                {
                    return new ValueTask<FluidValue>(task.Result ?? NilValue.Instance);
                }

                return Awaited(task);
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


            static async ValueTask<FluidValue> Awaited(ValueTask<FluidValue> task)
            {
                return await task ?? NilValue.Instance;
            }

            async ValueTask<FluidValue> AwaitedDirect(
                ValueTask<FluidValue> task,
                string memberName,
                TemplateContext ctx)
            {
                var directValue = await task;
                return directValue ?? await GetNestedValueAsync(memberName, ctx);
            }
        }

        private async ValueTask<FluidValue> GetNestedValueAsync(string name, TemplateContext context)
        {
            var members = name.Split(MemberSeparators);
            var target = Value;
            FluidValue value = null;
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

                value = await accessor.GetAsync(target, prop, context);
                target = value?.ToObjectValue();
            }

            return value ?? NilValue.Instance;
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
