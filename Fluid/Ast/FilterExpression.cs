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
            public FilterCollection Collection;
            public int Version;
            public FilterDelegate Filter;
        }

        private volatile FilterCacheEntry _filterCache;

        public override ValueTask<FluidValue> EvaluateAsync(TemplateContext context)
        {
            var arguments = _cachedArguments;

            if (arguments is null)
            {
                // First evaluation, or arguments that can't be cached because they aren't all literals.
                return EvaluateWithArgumentsAsync(context);
            }

            var inputTask = Input.EvaluateAsync(context);

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

            if (cache is not null && ReferenceEquals(cache.Collection, filters) && cache.Version == filters.Version)
            {
                filter = cache.Filter;
            }
            else
            {
                // Read the version before the lookup so a concurrent mutation invalidates the entry
                // on the next evaluation instead of being silently baked in.
                var version = filters.Version;
                filters.TryGetValue(Name, out filter);
                _filterCache = new FilterCacheEntry { Collection = filters, Version = version, Filter = filter };
            }

            if (filter is null)
            {
                // When a filter is not defined, return the input unless strict filters are enabled
                if (context.Options.StrictFilters)
                {
                    throw new FluidException($"Undefined filter '{Name}'");
                }

                return new ValueTask<FluidValue>(input);
            }

            return filter(input, arguments, context);
        }

        protected internal override Expression Accept(AstVisitor visitor) => visitor.VisitFilterExpression(this);
    }
}
