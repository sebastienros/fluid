using System;
using System.Collections.Generic;
using System.Linq;
using Fluid.Values;

namespace Fluid.Filters
{
    public static class ArrayFilters
    {
        public static FilterCollection WithArrayFilters(this FilterCollection filters)
        {
            filters.AddFilter("join", Join);
            filters.AddFilter("first", First);
            filters.AddFilter("last", Last);
            filters.AddFilter("concat", Concat);
            filters.AddFilter("map", Map);
            filters.AddFilter("reverse", Reverse);
            filters.AddFilter("size", Size);
            filters.AddFilter("sort", Sort);
            filters.AddFilter("sort_natural", SortNatural);
            filters.AddFilter("uniq", Uniq);
            filters.AddFilter("where", Where);
            return filters;
        }

        public static FluidValue Join(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            if (input.Type != FluidValues.Array)
            {
                return input;
            }

            return new StringValue(String.Join(arguments.At(0).ToStringValue(), input.Enumerate().Select(x => x.ToStringValue()).ToArray()));
        }

        public static FluidValue First(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            if (input.Type != FluidValues.Array)
            {
                return input;
            }

            return input.Enumerate().FirstOrDefault() ?? NilValue.Instance;
        }

        public static FluidValue Last(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            if (input.Type != FluidValues.Array)
            {
                return input;
            }

            return input.Enumerate().LastOrDefault() ?? NilValue.Instance;
        }

        public static FluidValue Concat(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            if (input.Type != FluidValues.Array)
            {
                return input;
            }

            if (arguments.At(0).Type != FluidValues.Array)
            {
                return input;
            }

            var concat = new List<FluidValue>();

            foreach(var item in input.Enumerate())
            {
                concat.Add(item);
            }

            foreach (var item in arguments.At(0).Enumerate())
            {
                concat.Add(item);
            }

            return new ArrayValue(concat);
        }

        public static FluidValue Map(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            if (input.Type != FluidValues.Array)
            {
                return input;
            }

            var member = arguments.At(0).ToStringValue();

            var list = new List<FluidValue>();

            foreach(var item in input.Enumerate())
            {
                list.Add(item.GetValueAsync(member, context).GetAwaiter().GetResult());
            }

            return new ArrayValue(list);
        }

        // https://github.com/Shopify/liquid/commit/842986a9721de11e71387732be51951285225977
        public static FluidValue Where(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            if (input.Type != FluidValues.Array)
            {
                return input;
            }

            // First argument is the property name to match
            var member = arguments.At(0).ToStringValue();

            // Second argument is the value to match, or 'true' if none is defined
            var targetValue = arguments.At(1).Or(BooleanValue.True);

            var list = new List<FluidValue>();

            foreach (var item in input.Enumerate())
            {
                var itemValueTask = item.GetValueAsync(member, context);

                var itemValue = itemValueTask.IsCompleted
                    ? itemValueTask.Result
                    : itemValueTask.GetAwaiter().GetResult()
                    ;

                if (itemValue.Equals(targetValue))
                {
                    list.Add(item);
                }
            }

            return new ArrayValue(list);
        }

        public static FluidValue Reverse(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            if (input.Type != FluidValues.Array)
            {
                return input;
            }

            return new ArrayValue(input.Enumerate().Reverse());
        }

        public static FluidValue Size(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            if (input.Type == FluidValues.Array)
            {
                return ((ArrayValue)input).GetValueAsync("size", context).GetAwaiter().GetResult();
            }

            if (input.Type == FluidValues.String)
            {
                return ((StringValue)input).GetValueAsync("size", context).GetAwaiter().GetResult();
            }

            return NilValue.Instance;
        }

        public static FluidValue Sort(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            if (arguments.Count > 0)
            {
                var member = arguments.At(0).ToStringValue();

                return new ArrayValue(input.Enumerate().OrderBy(x =>
                {
                    return x.GetValueAsync(member, context).GetAwaiter().GetResult().ToObjectValue();
                }).ToArray());
            }
            else
            {
                return new ArrayValue(input.Enumerate().OrderBy(x =>
                {
                    return x.ToStringValue();
                }, StringComparer.Ordinal).ToArray());
            }
        }

        public static FluidValue SortNatural(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            if (arguments.Count > 0)
            {
                var member = arguments.At(0).ToStringValue();

                return new ArrayValue(input.Enumerate().OrderBy(x =>
                {
                    return x.GetValueAsync(member, context).GetAwaiter().GetResult().ToObjectValue();
                }).ToArray());
            }
            else
            {
                return new ArrayValue(input.Enumerate().OrderBy(x =>
                {
                    return x.ToStringValue();
                }, StringComparer.OrdinalIgnoreCase).ToArray());
            }
        }

        public static FluidValue Uniq(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            return new ArrayValue(input.Enumerate().Distinct().ToArray());
        }
    }
}
