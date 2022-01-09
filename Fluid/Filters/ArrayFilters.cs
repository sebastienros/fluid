﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

        public static async ValueTask<FluidValue> Join(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            if (input.Type != FluidValues.Array)
            {
                return input;
            }

            var separator = arguments.At(0).ToStringValue();
            var values = (await input.EnumerateAsync(context)).Select(x => x.ToStringValue());
            var joined = string.Join(separator, values);
            return StringValue.Create(joined);
        }

        public static ValueTask<FluidValue> First(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            return input.GetValueAsync("first", context);
        }

        public static ValueTask<FluidValue> Last(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            return input.GetValueAsync("last", context);
        }

        public static async ValueTask<FluidValue> Concat(FluidValue input, FilterArguments arguments, TemplateContext context)
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

            foreach(var item in await input.EnumerateAsync(context))
            {
                concat.Add(item);
            }

            foreach (var item in await arguments.At(0).EnumerateAsync(context))
            {
                concat.Add(item);
            }

            return ArrayValue.Create(concat);
        }

        public static async ValueTask<FluidValue> Map(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            if (input.Type != FluidValues.Array)
            {
                return input;
            }

            var member = arguments.At(0).ToStringValue();

            var list = new List<FluidValue>();

            foreach(var item in await input.EnumerateAsync(context))
            {
                list.Add(await item.GetValueAsync(member, context));
            }

            return ArrayValue.Create(list);
        }

        public static async ValueTask<FluidValue> Reverse(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            if (input.Type == FluidValues.Array)
            {
                return ArrayValue.Create((await input.EnumerateAsync(context)).Reverse());
            }
            else if (input.Type == FluidValues.String)
            {
                var value = input.ToStringValue();
                if (String.IsNullOrEmpty(value))
                {
                    return StringValue.Empty;
                }
                else
                {
                    var valueAsArray = value.ToCharArray();
                    
                    Array.Reverse(valueAsArray);

                    return ArrayValue.Create(valueAsArray.Select(e => StringValue.Create(e.ToString())));
                }
            }
            else
            {
                return input;
            }
        }

        // https://github.com/Shopify/liquid/commit/842986a9721de11e71387732be51951285225977
        public static async ValueTask<FluidValue> Where(FluidValue input, FilterArguments arguments, TemplateContext context)
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

            foreach (var item in await input.EnumerateAsync(context))
            {
                var itemValue = await item.GetValueAsync(member, context);

                if (itemValue.Equals(targetValue))
                {
                    list.Add(item);
                }
            }

            return ArrayValue.Create(list);
        }

        public static ValueTask<FluidValue> Size(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            return input.GetValueAsync("size", context);
        }

        public static async ValueTask<FluidValue> Sort(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            if (arguments.Count > 0)
            {
                var member = arguments.At(0).ToStringValue();

                var values = new List<KeyValuePair<FluidValue, object>>();

                foreach (var item in await input.EnumerateAsync(context))
                {
                    values.Add(new KeyValuePair<FluidValue, object>(item, (await item.GetValueAsync(member, context)).ToObjectValue()));
                }

                var orderedValues = values
                    .OrderBy(x => x.Value)
                    .Select(x => x.Key)
                    .ToArray();

                return ArrayValue.Create(orderedValues);
            }
            else
            {
                return ArrayValue.Create((await input.EnumerateAsync(context)).OrderBy(x => x.ToStringValue(), StringComparer.Ordinal).ToArray());
            }
        }

        public static async ValueTask<FluidValue> SortNatural(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            if (arguments.Count > 0)
            {
                var member = arguments.At(0).ToStringValue();

                var values = new List<KeyValuePair<FluidValue, object>>();

                foreach (var item in await input.EnumerateAsync(context))
                {
                    values.Add(new KeyValuePair<FluidValue, object>(item, (await item.GetValueAsync(member, context)).ToObjectValue()));
                }

                var orderedValues = values
                    .OrderBy(x => x.Value)
                    .Select(x => x.Key)
                    .ToArray();

                return ArrayValue.Create(orderedValues);
            }
            else
            {
                return ArrayValue.Create((await input.EnumerateAsync(context)).OrderBy(x => x.ToStringValue(), StringComparer.OrdinalIgnoreCase));
            }
        }

        public static async ValueTask<FluidValue> Uniq(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            return ArrayValue.Create((await input.EnumerateAsync(context)).Distinct().ToArray());
        }
    }
}
