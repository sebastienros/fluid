using Fluid.Values;

namespace Fluid.Ast;

/// <summary>
/// Invokes a filter by name from a single call site, such as a <see cref="FilterExpression"/> or the code generated
/// for one, and remembers the resolved <see cref="FilterDelegate"/>.
/// </summary>
/// <remarks>
/// Filters are looked up by name in a dictionary shared by every call site, so a template that invokes the same
/// filter in a loop pays for the string hashing once instead of once per iteration. The cache is invalidated when
/// the collection instance or its version changes, so filters registered after the first render are still picked up.
/// Instances are thread-safe.
/// </remarks>
public sealed class FilterCallSite
{
    private sealed class CacheEntry
    {
        /// <summary>
        /// Marks a call site that misses too often to be worth caching -- typically one template
        /// rendered against several sets of options. Without it such a site would allocate a
        /// replacement entry on every filter invocation.
        /// </summary>
        public static readonly CacheEntry Disabled = new();

        public FilterCollection Collection;
        public int Version;
        public FilterDelegate Filter;
        public int Misses;
    }

    /// <summary>
    /// How many times a call site may re-resolve its filter before it stops caching.
    /// </summary>
    private const int MaxMisses = 4;

    private volatile CacheEntry _cache;

    public FilterCallSite(string name)
    {
        Name = name;
    }

    public string Name { get; }

    /// <summary>
    /// Invokes the filter, or returns the input when it is not defined and <see cref="TemplateOptions.StrictFilters"/>
    /// is not set.
    /// </summary>
    public ValueTask<FluidValue> InvokeAsync(FluidValue input, FilterArguments arguments, TemplateContext context)
    {
        var filters = context.Options.Filters;
        var cache = _cache;

        FilterDelegate filter;

        if (ReferenceEquals(cache, CacheEntry.Disabled))
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

            _cache = misses > MaxMisses
                ? CacheEntry.Disabled
                : new CacheEntry { Collection = filters, Version = version, Filter = filter, Misses = misses };
        }

        if (filter is null)
        {
            // When a filter is not defined, return the input unless strict filters are enabled
            if (context.Options.StrictFilters)
            {
                // Faulted rather than thrown: callers that start a render and await it later would
                // otherwise see the throw escape at the call site.
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
            // Same reason: a filter that throws before its first await faults the task.
            return Faulted(e);
        }
    }

    internal static ValueTask<FluidValue> Faulted(Exception exception)
    {
        return new ValueTask<FluidValue>(Task.FromException<FluidValue>(exception));
    }
}
