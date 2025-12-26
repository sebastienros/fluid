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
            filters.AddFilter("sum", Sum);
            filters.AddFilter("find", Find);
            filters.AddFilter("find_index", FindIndex);
            filters.AddFilter("has", Has);
            filters.AddFilter("reject", Reject);
            return filters;
        }

        public static async ValueTask<FluidValue> Join(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            if (input.Type != FluidValues.Array)
            {
                return input;
            }

            var separator = arguments.At(0).ToStringValue();
            var values = input.EnumerateAsync(context).Select(x => x.ToStringValue());
            var joined = string.Join(separator, await values.ToListAsync());
            return new StringValue(joined);
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
            var arg = arguments.At(0);

            if (input.Type != FluidValues.Array && arg.Type != FluidValues.Array)
            {
                return input;
            }

            var concat = new List<FluidValue>();

            if (input.Type == FluidValues.Array)
            {
                await foreach (var item in input.EnumerateAsync(context))
                {
                    concat.Add(item);
                }
            }
            else
            {
                concat.Add(input);
            }

            if (arg.Type == FluidValues.Array)
            {
                await foreach (var item in arg.EnumerateAsync(context))
                {
                    concat.Add(item);
                }
            }
            else
            {
                concat.Add(arg);
            }

            return new ArrayValue(concat);
        }

        public static async ValueTask<FluidValue> Map(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            if (input.Type != FluidValues.Array)
            {
                return input;
            }

            var member = arguments.At(0).ToStringValue();

            var list = new List<FluidValue>();

            await foreach (var item in input.EnumerateAsync(context))
            {
                list.Add(await item.GetValueAsync(member, context));
            }

            return new ArrayValue(list);
        }

        public static async ValueTask<FluidValue> Reverse(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            if (input.Type == FluidValues.Array)
            {
                return new ArrayValue(await input.EnumerateAsync(context).Reverse().ToArrayAsync());
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

                    return new ArrayValue(valueAsArray.Select(e => new StringValue(e.ToString())).ToArray());
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
            var targetValue = arguments.At(1);

            List<FluidValue> list = null;

            await foreach (var item in input.EnumerateAsync(context))
            {
                var itemValue = await item.GetValueAsync(member, context);

                var match = false;

                // If not target value is defined, check truthiness of item
                if (targetValue.IsNil()) 
                { 
                    match = itemValue.ToBooleanValue(context); 
                }
                else if (targetValue.Equals(itemValue))
                {
                    match = true;
                }

                if (match)
                {
                    list ??= [];
                    list.Add(item);
                }
            }

            return new ArrayValue(list);
        }

        public static async ValueTask<FluidValue> Find(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            if (input.Type != FluidValues.Array)
            {
                return input;
            }

            // First argument is the property name to match
            var member = arguments.At(0).ToStringValue();

            // Second argument is the value to match
            var targetValue = arguments.At(1);
            if (targetValue.IsNil())
            {
                return NilValue.Instance;
            }

            FluidValue result = NilValue.Instance;

            await foreach (var item in input.EnumerateAsync(context))
            {
                var itemValue = await item.GetValueAsync(member, context);

                if (targetValue.Equals(itemValue))
                {
                    result = item;
                    break;
                }
            }

            return result;
        }

        public static async ValueTask<FluidValue> FindIndex(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            if (input.Type != FluidValues.Array)
            {
                return input;
            }

            // First argument is the property name to match
            var member = arguments.At(0).ToStringValue();

            // Second argument is the value to match
            var targetValue = arguments.At(1);
            if (targetValue.IsNil())
            {
                return NilValue.Instance;
            }

            FluidValue result = NilValue.Instance;
            var index = 0;

            await foreach (var item in input.EnumerateAsync(context))
            {
                var itemValue = await item.GetValueAsync(member, context);

                if (targetValue.Equals(itemValue))
                {
                    result = NumberValue.Create(index);
                    break;
                }

                index++;
            }

            return result;
        }

        public static async ValueTask<FluidValue> Has(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            var result = await Find(input, arguments, context);

            return result.Equals(NilValue.Instance) ? BooleanValue.False : BooleanValue.True;
        }

        public static async ValueTask<FluidValue> Reject(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            if (input.Type != FluidValues.Array)
            {
                return input;
            }

            // First argument is the property name to match
            var member = arguments.At(0).ToStringValue();

            // Second argument is the value to not match, or 'true' if none is defined
            var targetValue = arguments.At(1);
            if (targetValue.IsNil())
            {
                targetValue = BooleanValue.True;
            }

            var list = new List<FluidValue>();

            await foreach (var item in input.EnumerateAsync(context))
            {
                var itemValue = await item.GetValueAsync(member, context);

                if (!targetValue.Equals(itemValue))
                {
                    list.Add(item);
                }
            }

            return new ArrayValue(list);
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

                await foreach (var item in input.EnumerateAsync(context))
                {
                    values.Add(new KeyValuePair<FluidValue, object>(item, (await item.GetValueAsync(member, context)).ToObjectValue()));
                }

                var orderedValues = values
                    .OrderBy(x => x.Value)
                    .Select(x => x.Key)
                    .ToArray();

                return new ArrayValue(orderedValues);
            }
            else
            {
                return new ArrayValue(await input.EnumerateAsync(context).OrderBy(x => x.ToStringValue(), StringComparer.Ordinal).ToArrayAsync());
            }
        }

        public static async ValueTask<FluidValue> SortNatural(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            if (arguments.Count > 0)
            {
                var member = arguments.At(0).ToStringValue();

                var values = new List<KeyValuePair<FluidValue, object>>();

                await foreach (var item in input.EnumerateAsync(context))
                {
                    values.Add(new KeyValuePair<FluidValue, object>(item, (await item.GetValueAsync(member, context)).ToObjectValue()));
                }

                var orderedValues = values
                    .OrderBy(x => x.Value)
                    .Select(x => x.Key)
                    .ToArray();

                return new ArrayValue(orderedValues);
            }
            else
            {
                return new ArrayValue(await input.EnumerateAsync(context).OrderBy(x => x.ToStringValue(), StringComparer.OrdinalIgnoreCase).ToArrayAsync());
            }
        }

        public static async ValueTask<FluidValue> Uniq(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            return new ArrayValue(await input.EnumerateAsync(context).Distinct().ToArrayAsync());
        }

        public static async ValueTask<FluidValue> Sum(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            if (arguments.Count == 0)
            {
                var numbers = input.EnumerateAsync(context).Select(x => x switch
                {
                    ArrayValue => Sum(x, arguments, context).Result.ToNumberValue(),
                    NumberValue or StringValue => x.ToNumberValue(),
                    _ => 0
                });

                return NumberValue.Create(await numbers.SumAsync());
            }

            var member = arguments.At(0);

            var sumList = new List<decimal>();

            await foreach (var item in input.EnumerateAsync(context))
            {
                switch (item)
                {
                    case ArrayValue:
                        sumList.Add(Sum(item, arguments, context).Result.ToNumberValue());
                        break;
                    case ObjectValue:
                        {
                            var value = await item.GetValueAsync(member.ToStringValue(), context);
                            sumList.Add(value.ToNumberValue());
                            break;
                        }
                }
            }

            return NumberValue.Create(sumList.Sum());
        }
    }
}
