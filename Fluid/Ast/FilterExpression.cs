using System;
using Fluid.Values;

namespace Fluid.Ast
{
    public sealed class FilterExpression : Expression
    {
        public FilterExpression(Expression input, string name, IReadOnlyList<FilterArgument> parameters)
        {
            Input = input;
            Name = name;
            Parameters = parameters ?? [];
        }

        public Expression Input { get; }
        public string Name { get; }
        public IReadOnlyList<FilterArgument> Parameters { get; }

        private volatile bool _canBeCached = true;
        private volatile FilterArguments _cachedArguments;

        // Monomorphic inline cache of the resolved filter. Filters are looked up by name in a dictionary
        // shared by every call site, so a template that invokes the same filter in a loop pays for the
        // string hashing once instead of once per iteration. The cache is invalidated when the collection
        // instance or its version changes, so filters registered after the first render are still picked up.
        private sealed class FilterCacheEntry
        {
            /// <summary>
            /// Marks a call site that misses too often to be worth caching -- typically one template
            /// rendered against several sets of options. Without it such a site would allocate a
            /// replacement entry on every filter invocation.
            /// </summary>
            public static readonly FilterCacheEntry Disabled = new();

            public FilterCollection Collection;
            public int Version;
            public FilterDelegate Filter;
            public int Misses;
        }

        /// <summary>
        /// How many times a call site may re-resolve its filter before it stops caching.
        /// </summary>
        private const int MaxMisses = 4;

        private volatile FilterCacheEntry _filterCache;

        public override ValueTask<FluidValue> EvaluateAsync(TemplateContext context)
        {
            var arguments = _cachedArguments;

            if (arguments is null)
            {
                // First evaluation, or arguments that can't be cached because they aren't all literals.
                return EvaluateWithArgumentsAsync(context);
            }

            ValueTask<FluidValue> inputTask;

            try
            {
                inputTask = Input.EvaluateAsync(context);
            }
            catch (Exception e)
            {
                // EvaluateAsync used to be async, so even a synchronous input failure faulted its task.
                return Faulted(e);
            }

            if (!inputTask.IsCompletedSuccessfully)
            {
                return AwaitedInput(inputTask, arguments, context);
            }

            // Nothing was awaited: invoke the filter directly and forward its ValueTask, so a synchronous
            // filter (the vast majority) doesn't allocate an async state machine.
            return InvokeFilter(inputTask.Result, arguments, context);
        }

        private async ValueTask<FluidValue> EvaluateWithArgumentsAsync(TemplateContext context)
        {
            // The arguments can be cached if all the parameters are LiteralExpression
            var arguments = new FilterArguments();

            foreach (var parameter in Parameters)
            {
                _canBeCached = _canBeCached && parameter.Expression is LiteralExpression;
                arguments.Add(parameter.Name, await parameter.Expression.EvaluateAsync(context));
            }

            // Can we cache it?
            if (_canBeCached)
            {
                _cachedArguments = arguments;
            }

            var input = await Input.EvaluateAsync(context);

            return await InvokeFilter(input, arguments, context);
        }

        private async ValueTask<FluidValue> AwaitedInput(ValueTask<FluidValue> inputTask, FilterArguments arguments, TemplateContext context)
        {
            var input = await inputTask;
            return await InvokeFilter(input, arguments, context);
        }

        private ValueTask<FluidValue> InvokeFilter(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            var filters = context.Options.Filters;
            var cache = _filterCache;

            FilterDelegate filter;

            if (ReferenceEquals(cache, FilterCacheEntry.Disabled))
            {
                filters.TryGetValue(Name, out filter);
            }
            else if (cache is not null && ReferenceEquals(cache.Collection, filters) && cache.Version == filters.Version)
            {
                filter = cache.Filter;
            }
            else
            {
                // Read the version before the lookup so a concurrent mutation invalidates the entry
                // on the next evaluation instead of being silently baked in.
                var version = filters.Version;
                filters.TryGetValue(Name, out filter);

                var misses = (cache?.Misses ?? 0) + 1;

                _filterCache = misses > MaxMisses
                    ? FilterCacheEntry.Disabled
                    : new FilterCacheEntry { Collection = filters, Version = version, Filter = filter, Misses = misses };
            }

            if (filter is null)
            {
                // When a filter is not defined, return the input unless strict filters are enabled
                if (context.Options.StrictFilters)
                {
                    // Faulted rather than thrown: this method is no longer async, and callers that start
                    // a render and await it later would otherwise see the throw escape at the call site.
                    return Faulted(new FluidException($"Undefined filter '{Name}'"));
                }

                return new ValueTask<FluidValue>(input);
            }

            try
            {
                return filter(input, arguments, context);
            }
            catch (Exception e)
            {
                // Same reason: a filter that throws before its first await used to fault the task.
                return Faulted(e);
            }
        }

        private static ValueTask<FluidValue> Faulted(Exception exception)
        {
            return new ValueTask<FluidValue>(Task.FromException<FluidValue>(exception));
        }

        protected internal override Expression Accept(AstVisitor visitor) => visitor.VisitFilterExpression(this);
    }
}
