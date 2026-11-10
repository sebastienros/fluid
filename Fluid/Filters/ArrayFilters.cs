using Fluid.Utils;
using Fluid.Values;

namespace Fluid.Filters;

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

    public static ValueTask<FluidValue> Join(FluidValue input, FilterArguments arguments, TemplateContext context)
    {
        LiquidException.ThrowFilterArgumentsCount("join", min: 0, max: 1, arguments);

        if (input.Type != FluidValues.Array)
        {
            // Like Liquid, any other value is a one-item array, joined into its text, and nil an empty array.
            return input.IsNil() ? StringValue.Empty : StringValue.Create(input.ToStringValue(context));
        }

        var separator = arguments.Count > 0 ? arguments.At(0).ToStringValue(context) : " ";
        var list = new List<string>();

        // Indexed arrays are read by index, without an async enumerator, unless they hold a nested array that isn't.
        if (input is IFluidArray indexed)
        {
            long outputLength = 0;

            if (TryAddJoinedItems(indexed, list, separator, ref outputLength, context, depth: 0))
            {
                return StringValue.Create(string.Join(separator, list));
            }

            list.Clear();
        }

        return JoinAsync(input, separator, list, context);

        static async ValueTask<FluidValue> JoinAsync(FluidValue input, string separator, List<string> list, TemplateContext context)
        {
            long outputLength = 0;

            await foreach (var item in EnumerateFlattenedAsync(input, context))
            {
                AddJoinedItem(item, list, separator, ref outputLength, context);
            }

            return StringValue.Create(string.Join(separator, list));
        }
    }

    // Like Liquid's InputIterator, nested arrays are flattened.
    private static bool TryAddJoinedItems(IFluidArray items, List<string> list, string separator, ref long outputLength, TemplateContext context, int depth)
    {
        if (depth > 100)
        {
            ExceptionHelper.ThrowRecursiveValueException();
        }

        for (var i = 0; i < items.Count; i++)
        {
            // Like the enumeration of an array, each item is a step.
            context.IncrementSteps();
            var item = items[i] ?? NilValue.Instance;

            if (item.Type == FluidValues.Array && item is not RangeValue)
            {
                if (item is not IFluidArray nested || !TryAddJoinedItems(nested, list, separator, ref outputLength, context, depth + 1))
                {
                    return false;
                }

                continue;
            }

            AddJoinedItem(item, list, separator, ref outputLength, context);
        }

        return true;
    }

    private static void AddJoinedItem(FluidValue item, List<string> list, string separator, ref long outputLength, TemplateContext context)
    {
        // Nil items are empty strings, so that their separators remain.
        var s = item.IsNil() ? string.Empty : item.ToStringValue(context);

        context.EnsureCollectionSize((long)list.Count + 1);
        outputLength += s.Length;
        if (list.Count > 0)
        {
            outputLength += separator.Length;
        }
        context.EnsureOutputSize(outputLength);
        list.Add(s);
    }

    public static ValueTask<FluidValue> First(FluidValue input, FilterArguments arguments, TemplateContext context)
    {
        LiquidException.ThrowFilterArgumentsCount("first", expected: 0, arguments);

        // Like Ruby's Range#first, the start of a range is returned even if it is empty.
        if (input is RangeValue range)
        {
            return NumberValue.Create(range.Start);
        }

        if (input.Type == FluidValues.Array)
        {
            // Indexed arrays answer by index, without an async enumerator.
            if (input is not IFluidArray values)
            {
                return GetFirstFromArrayAsync(input, context);
            }

            if (values.Count == 0)
            {
                return NilValue.Instance;
            }

            return new ValueTask<FluidValue>(values[0] ?? NilValue.Instance);
        }

        if (input.Type == FluidValues.Dictionary)
        {
            return input.GetValueAsync("first", context);
        }

        if (input.Type == FluidValues.String)
        {
            return StringValue.FirstCharacter(input.ToStringValue(context));
        }

        return NilValue.Instance;
    }

    public static ValueTask<FluidValue> Last(FluidValue input, FilterArguments arguments, TemplateContext context)
    {
        LiquidException.ThrowFilterArgumentsCount("last", expected: 0, arguments);

        if (input is RangeValue range)
        {
            return NumberValue.Create(range.End);
        }

        if (input.Type == FluidValues.Array)
        {
            // Indexed arrays answer by index, instead of walking every item to reach the last one.
            if (input is not IFluidArray values)
            {
                return GetLastFromArrayAsync(input, context);
            }

            if (values.Count == 0)
            {
                return NilValue.Instance;
            }

            return new ValueTask<FluidValue>(values[values.Count - 1] ?? NilValue.Instance);
        }

        if (input.Type == FluidValues.String)
        {
            return StringValue.LastCharacter(input.ToStringValue(context));
        }

        return NilValue.Instance;
    }

    private static async ValueTask<FluidValue> GetFirstFromArrayAsync(FluidValue input, TemplateContext context)
    {
        await foreach (var item in input.EnumerateAsync(context))
        {
            return item;
        }

        return NilValue.Instance;
    }

    private static async ValueTask<FluidValue> GetLastFromArrayAsync(FluidValue input, TemplateContext context)
    {
        FluidValue last = NilValue.Instance;

        await foreach (var item in input.EnumerateAsync(context))
        {
            last = item;
        }

        return last;
    }

    public static async ValueTask<FluidValue> Concat(FluidValue input, FilterArguments arguments, TemplateContext context)
    {
        LiquidException.ThrowFilterArgumentsCount("concat", expected: 1, arguments);

        var arg = arguments.At(0);

        if (arg.Type != FluidValues.Array)
        {
            throw new LiquidException("concat: argument must be an array");
        }

        var concat = new List<FluidValue>();

        if (input.Type == FluidValues.Array)
        {
            await foreach (var item in EnumerateFlattenedAsync(input, context))
            {
                context.EnsureCollectionSize((long)concat.Count + 1);
                concat.Add(item);
            }
        }
        else if (input.Type != FluidValues.Nil)
        {
            context.EnsureCollectionSize(1);
            concat.Add(input);
        }

        // Like Liquid, only the input is flattened: nested arrays in the argument stay as items.
        await foreach (var item in arg.EnumerateAsync(context))
        {
            context.EnsureCollectionSize((long)concat.Count + 1);
            concat.Add(item);
        }

        return ArrayValue.Create(concat);
    }

    public static async ValueTask<FluidValue> Map(FluidValue input, FilterArguments arguments, TemplateContext context)
    {
        LiquidException.ThrowFilterArgumentsCount("map", expected: 1, arguments);

        var member = arguments.At(0);

        // If the member argument is nil or undefined, return empty array
        if (member.IsNil())
        {
            return ArrayValue.Empty;
        }

        // Nil/undefined input maps to an empty result.
        if (input.IsNil())
        {
            return ArrayValue.Empty;
        }

        var list = new List<FluidValue>();

        await foreach (var item in EnumerateInputAsync(input, context))
        {
            context.EnsureCollectionSize((long)list.Count + 1);

            // Like Liquid, an item that can't be indexed (nil, a boolean, a float) maps to nil.
            list.Add(IsIndexable(item, context) ? await GetPropertyAsync(item, member, context) : NilValue.Instance);
        }

        return ArrayValue.Create(list);
    }

    /// <summary>
    /// Whether Liquid's array filters can read a property of an item, which they do with Ruby's <c>item[property]</c>:
    /// hashes, drops, strings and integers have a <c>[]</c> method, nil, booleans, floats and ranges don't.
    /// </summary>
    internal static bool IsIndexable(FluidValue item, TemplateContext context)
    {
        return item.Type switch
        {
            FluidValues.Dictionary or FluidValues.Object or FluidValues.String => true,
            FluidValues.Number => NumberValue.GetScale(item.ToNumberValue(context)) == 0,
            FluidValues.Array => item is not RangeValue,
            _ => false,
        };
    }

    /// <summary>
    /// Reads the property of an item like Liquid's <c>item[property]</c>: on a hash, size, first and last are keys
    /// like any other, missing ones are nil. On a string it is Ruby's <c>String#[]</c>: a string property is returned
    /// when the item contains it and is nil otherwise, a number is the index of a character. On an integer it is a bit,
    /// and any other property is an error.
    /// </summary>
    internal static ValueTask<FluidValue> GetPropertyAsync(FluidValue item, FluidValue property, TemplateContext context)
    {
        return item switch
        {
            DictionaryValue dictionary => new ValueTask<FluidValue>(dictionary.GetKey(property.ToStringValue(context))),
            { Type: FluidValues.String } => new ValueTask<FluidValue>(GetStringProperty(item.ToStringValue(context), property, context)),
            { Type: FluidValues.Number } => item.GetIndexAsync(ToBitIndex(property, context), context),
            _ => item.GetValueAsync(property.ToStringValue(context), context),
        };
    }

    // Like Ruby's Integer#[], a float index is truncated.
    private static FluidValue ToBitIndex(FluidValue property, TemplateContext context)
    {
        if (property.Type == FluidValues.Number)
        {
            var index = property.ToNumberValue(context);

            if (NumberValue.GetScale(index) != 0)
            {
                return NumberValue.Create(decimal.Truncate(index));
            }
        }

        return property;
    }

    // Ruby's String#[]: "banana"["an"] is "an", "banana"["x"] is nil, "banana"[1] is "a".
    private static FluidValue GetStringProperty(string value, FluidValue property, TemplateContext context)
    {
        if (property.Type == FluidValues.String)
        {
            return value.Contains(property.ToStringValue(context)) ? property : NilValue.Instance;
        }

        if (property.Type != FluidValues.Number)
        {
            throw new LiquidException($"cannot select the property '{property.ToStringValue(context)}'");
        }

        // Like Ruby, a float index is truncated and a negative one counts from the end, in code points.
        var index = decimal.Truncate(property.ToNumberValue(context));
        var hasSurrogates = CodePoints.HasSurrogates(value.AsSpan());
        var length = hasSurrogates ? CodePoints.Count(value) : value.Length;

        if (index < 0)
        {
            index += length;
        }

        if (index < 0 || index >= length)
        {
            return NilValue.Instance;
        }

        if (!hasSurrogates)
        {
            return StringValue.Create(value[(int)index]);
        }

        var start = CodePoints.ToCharIndex(value, (int)index);
        return StringValue.Create(value.Substring(start, CodePoints.Width(value, start)));
    }

    /// <summary>
    /// Enumerates the input of an array filter like Liquid's <c>InputIterator</c>: an array is flattened, nil has no
    /// items and any other value is a single item.
    /// </summary>
    internal static IAsyncEnumerable<FluidValue> EnumerateInputAsync(FluidValue input, TemplateContext context)
    {
        return input.Type == FluidValues.Array ? EnumerateFlattenedAsync(input, context) : EnumerateItemAsync(input);

        static async IAsyncEnumerable<FluidValue> EnumerateItemAsync(FluidValue item)
        {
            if (!item.IsNil())
            {
                yield return item;
            }

            await Task.CompletedTask;
        }
    }

    /// <summary>
    /// Enumerates an array like Liquid's <c>InputIterator</c>, which flattens nested arrays: their items are
    /// enumerated instead. Ranges, hashes and any other value are items.
    /// </summary>
    internal static async IAsyncEnumerable<FluidValue> EnumerateFlattenedAsync(FluidValue value, TemplateContext context, int depth = 0)
    {
        // Like Ruby's Array#flatten, which fails on recursive arrays.
        if (depth > 100)
        {
            ExceptionHelper.ThrowRecursiveValueException();
        }

        // The items of an ArrayValue are read by index, without allocating its enumerator.
        if (value is ArrayValue array)
        {
            var values = array.Values;

            for (var i = 0; i < values.Count; i++)
            {
                // Like the enumeration of an array, each item is a step.
                context?.IncrementSteps();
                var item = values[i];

                if (item.Type == FluidValues.Array && item is not RangeValue)
                {
                    await foreach (var nested in EnumerateFlattenedAsync(item, context, depth + 1))
                    {
                        yield return nested;
                    }
                }
                else
                {
                    yield return item;
                }
            }

            yield break;
        }

        await foreach (var item in value.EnumerateAsync(context))
        {
            if (item.Type == FluidValues.Array && item is not RangeValue)
            {
                await foreach (var nested in EnumerateFlattenedAsync(item, context, depth + 1))
                {
                    yield return nested;
                }
            }
            else
            {
                yield return item;
            }
        }
    }

    public static async ValueTask<FluidValue> Reverse(FluidValue input, FilterArguments arguments, TemplateContext context)
    {
        LiquidException.ThrowFilterArgumentsCount("reverse", expected: 0, arguments);

        if (input.Type == FluidValues.Array)
        {
            return ArrayValue.Create(await EnumerateFlattenedAsync(input, context).Reverse().ToArrayAsync());
        }
        else if (input.Type == FluidValues.String)
        {
            var value = input.ToStringValue(context);
            if (String.IsNullOrEmpty(value))
            {
                return StringValue.Empty;
            }
            else
            {
                // The characters are code points: a surrogate pair stays together.
                var hasSurrogates = CodePoints.HasSurrogates(value.AsSpan());
                var count = hasSurrogates ? CodePoints.Count(value) : value.Length;
                context.EnsureCollectionSize(count);
                var items = new FluidValue[count];

                for (int i = 0, j = count - 1; i < value.Length; j--)
                {
                    var width = hasSurrogates ? CodePoints.Width(value, i) : 1;
                    items[j] = width == 1 ? StringValue.Create(value[i]) : StringValue.Create(value.Substring(i, width));
                    i += width;
                }

                return ArrayValue.Create(items);
            }
        }
        else
        {
            return AsOneItemArray(input);
        }
    }

    /// <summary>
    /// Like Liquid's InputIterator, a value that isn't an array (a hash or a scalar) is a one-item array, and nil an empty array.
    /// </summary>
    private static ArrayValue AsOneItemArray(FluidValue input)
    {
        return input.IsNil() ? ArrayValue.Empty : ArrayValue.Create([input]);
    }

    // https://github.com/Shopify/liquid/commit/842986a9721de11e71387732be51951285225977
    public static ValueTask<FluidValue> Where(FluidValue input, FilterArguments arguments, TemplateContext context)
    {
        LiquidException.ThrowFilterArgumentsCount("where", min: 1, max: 2, arguments);

        // First argument is the property name to match. If it is nil/undefined, return empty array
        var member = arguments.At(0);

        if (member.IsNil())
        {
            return ArrayValue.Empty;
        }

        return FilterArrayAsync(input, member, arguments.At(1), FilterArrayMode.Select, context);
    }

    public static ValueTask<FluidValue> Find(FluidValue input, FilterArguments arguments, TemplateContext context)
    {
        LiquidException.ThrowFilterArgumentsCount("find", min: 1, max: 2, arguments);

        return FilterArrayAsync(input, arguments.At(0), arguments.At(1), FilterArrayMode.Find, context);
    }

    public static ValueTask<FluidValue> FindIndex(FluidValue input, FilterArguments arguments, TemplateContext context)
    {
        LiquidException.ThrowFilterArgumentsCount("find_index", min: 1, max: 2, arguments);

        return FilterArrayAsync(input, arguments.At(0), arguments.At(1), FilterArrayMode.FindIndex, context);
    }

    public static ValueTask<FluidValue> Has(FluidValue input, FilterArguments arguments, TemplateContext context)
    {
        LiquidException.ThrowFilterArgumentsCount("has", min: 1, max: 2, arguments);

        return FilterArrayAsync(input, arguments.At(0), arguments.At(1), FilterArrayMode.Any, context);
    }

    public static ValueTask<FluidValue> Reject(FluidValue input, FilterArguments arguments, TemplateContext context)
    {
        LiquidException.ThrowFilterArgumentsCount("reject", min: 1, max: 2, arguments);

        // First argument is the property name to match
        var member = arguments.At(0);

        if (member.IsNil())
        {
            return ArrayValue.Empty;
        }

        return FilterArrayAsync(input, member, arguments.At(1), FilterArrayMode.Reject, context);
    }

    private enum FilterArrayMode
    {
        Select,
        Reject,
        Any,
        Find,
        FindIndex,
    }

    /// <summary>
    /// Implements where, reject, has, find and find_index like Liquid's <c>filter_array</c>: the input is enumerated
    /// like <c>InputIterator</c> (nested arrays are flattened, a hash or a string is a one-item array), an item matches
    /// when <c>item[property]</c> is truthy or, with a target value, equals it, and an item that can't be indexed
    /// (nil, a boolean, a float) makes the filter return nil once it is reached.
    /// </summary>
    private static async ValueTask<FluidValue> FilterArrayAsync(FluidValue input, FluidValue property, FluidValue targetValue, FilterArrayMode mode, TemplateContext context)
    {
        // Like Liquid, the empty and blank literals are an empty string when they are an argument, not nil.
        if (targetValue.Type is FluidValues.Empty or FluidValues.Blank)
        {
            targetValue = StringValue.Empty;
        }

        var hasTargetValue = !targetValue.IsNil();
        List<FluidValue> list = null;
        var index = 0;

        await foreach (var item in EnumerateInputAsync(input, context))
        {
            if (!IsIndexable(item, context))
            {
                return NilValue.Instance;
            }

            var value = await GetPropertyAsync(item, property, context);
            var match = hasTargetValue ? targetValue.Equals(value) : value.ToBooleanValue(context);

            switch (mode)
            {
                case FilterArrayMode.Any when match:
                    return BooleanValue.True;
                case FilterArrayMode.Find when match:
                    return item;
                case FilterArrayMode.FindIndex when match:
                    return NumberValue.Create(index);
                case FilterArrayMode.Select when match:
                case FilterArrayMode.Reject when !match:
                    list ??= [];
                    context.EnsureCollectionSize((long)list.Count + 1);
                    list.Add(item);
                    break;
            }

            index++;
        }

        return mode switch
        {
            FilterArrayMode.Any => BooleanValue.False,
            FilterArrayMode.Find or FilterArrayMode.FindIndex => NilValue.Instance,
            _ => list != null ? ArrayValue.Create(list) : ArrayValue.Empty,
        };
    }

    public static ValueTask<FluidValue> Size(FluidValue input, FilterArguments arguments, TemplateContext context)
    {
        LiquidException.ThrowFilterArgumentsCount("size", expected: 0, arguments);

        var task = input.GetValueAsync("size", context);

        if (task.IsCompletedSuccessfully)
        {
            return OrZero(task.Result);
        }

        return Awaited(task);

        // Like Liquid, a value without a size (a number, a boolean, nil) has a size of 0.
        static FluidValue OrZero(FluidValue size) => size.IsNil() ? NumberValue.Zero : size;

        static async ValueTask<FluidValue> Awaited(ValueTask<FluidValue> task) => OrZero(await task);
    }

    /// <summary>
    /// Reads the property that sort and sort_natural order the items by. Like Liquid, nothing is sorted (null) when an
    /// item can't be indexed, and the property of a single item isn't read.
    /// </summary>
    private static async ValueTask<List<KeyValuePair<FluidValue, FluidValue>>> GetSortKeysAsync(FluidValue input, FluidValue member, TemplateContext context)
    {
        var items = await EnumerateFlattenedAsync(input, context).ToListAsync();
        var values = new List<KeyValuePair<FluidValue, FluidValue>>(items.Count);

        foreach (var item in items)
        {
            if (!IsIndexable(item, context))
            {
                return null;
            }
        }

        foreach (var item in items)
        {
            values.Add(new KeyValuePair<FluidValue, FluidValue>(item, items.Count > 1 ? await GetPropertyAsync(item, member, context) : NilValue.Instance));
        }

        return values;
    }

    public static async ValueTask<FluidValue> Sort(FluidValue input, FilterArguments arguments, TemplateContext context)
    {
        LiquidException.ThrowFilterArgumentsCount("sort", min: 0, max: 1, arguments);

        if (input.Type != FluidValues.Array)
        {
            // Like Liquid, nothing is sorted by a property when the item can't be indexed.
            if (!arguments.At(0).IsNil() && !input.IsNil() && !IsIndexable(input, context))
            {
                return NilValue.Instance;
            }

            return AsOneItemArray(input);
        }

        // If argument is provided but is nil, treat as no argument
        if (arguments.Count > 0 && !arguments.At(0).IsNil())
        {
            var values = await GetSortKeysAsync(input, arguments.At(0), context);

            if (values == null)
            {
                return NilValue.Instance;
            }

            // Sort with case-sensitive comparison and numbers compared as numbers, placing items with missing keys at the end
            var orderedValues = values
                .OrderBy(x => x.Value.IsNil() ? 1 : 0)
                .ThenBy(x => x.Value, new LiquidSortComparer(caseSensitive: true, compareNumbersAsStrings: false, context))
                .Select(x => x.Key)
                .ToArray();

            return ArrayValue.Create(orderedValues);
        }
        else
        {
            var values = await EnumerateFlattenedAsync(input, context).ToListAsync();

            // Check for incompatible types. Like Ruby, a hash can't be compared with a number or a string.
            bool hasArray = false, hasObject = false, hasNumber = false, hasString = false;
            foreach (var v in values)
            {
                if (v.Type == FluidValues.Array) hasArray = true;
                else if (v.Type is FluidValues.Object or FluidValues.Dictionary) hasObject = true;
                else if (v.Type == FluidValues.Number) hasNumber = true;
                else if (v.Type == FluidValues.String) hasString = true;
            }

            if ((hasArray || hasObject) && (hasNumber || hasString))
            {
                throw new LiquidException("Cannot sort array with incompatible types");
            }

            return ArrayValue.Create(values.OrderBy(x => x, new LiquidSortComparer(caseSensitive: true, compareNumbersAsStrings: false, context)).ToArray());
        }
    }

    public static async ValueTask<FluidValue> SortNatural(FluidValue input, FilterArguments arguments, TemplateContext context)
    {
        LiquidException.ThrowFilterArgumentsCount("sort_natural", min: 0, max: 1, arguments);

        if (input.Type != FluidValues.Array)
        {
            // Like Liquid, nothing is sorted by a property when the item can't be indexed.
            if (!arguments.At(0).IsNil() && !input.IsNil() && !IsIndexable(input, context))
            {
                return NilValue.Instance;
            }

            return AsOneItemArray(input);
        }

        // If argument is provided but is nil, treat as no argument
        if (arguments.Count > 0 && !arguments.At(0).IsNil())
        {
            var values = await GetSortKeysAsync(input, arguments.At(0), context);

            if (values == null)
            {
                return NilValue.Instance;
            }

            // Sort with case-insensitive comparison, placing items with missing keys at the end
            var orderedValues = values
                .OrderBy(x => x.Value.IsNil() ? 1 : 0)
                .ThenBy(x => x.Value, new LiquidSortComparer(caseSensitive: false, compareNumbersAsStrings: true, context))
                .Select(x => x.Key)
                .ToArray();

            return ArrayValue.Create(orderedValues);
        }
        else
        {
            var values = await EnumerateFlattenedAsync(input, context).ToListAsync();

            // Check for incompatible types
            bool hasArray = false, hasObject = false, hasNumber = false, hasString = false;
            foreach (var v in values)
            {
                if (v.Type == FluidValues.Array) hasArray = true;
                else if (v.Type == FluidValues.Object) hasObject = true;
                else if (v.Type == FluidValues.Number) hasNumber = true;
                else if (v.Type == FluidValues.String) hasString = true;
            }

            if ((hasArray || hasObject) && (hasNumber || hasString))
            {
                throw new LiquidException("Cannot sort array with incompatible types");
            }

            return ArrayValue.Create(values.OrderBy(x => x, new LiquidSortComparer(caseSensitive: false, compareNumbersAsStrings: true, context)).ToArray());
        }
    }

    public static async ValueTask<FluidValue> Uniq(FluidValue input, FilterArguments arguments, TemplateContext context)
    {
        LiquidException.ThrowFilterArgumentsCount("uniq", min: 0, max: 1, arguments);

        // Like Liquid, nil is an empty array and any other value a one-item array.
        if (input.Type != FluidValues.Array)
        {
            return AsOneItemArray(input);
        }

        // Like Liquid, a nil property is no property.
        if (arguments.At(0).IsNil())
        {
            return ArrayValue.Create(await EnumerateFlattenedAsync(input, context).Distinct().ToArrayAsync());
        }

        var property = arguments.At(0);
        var seen = new HashSet<FluidValue>();
        var result = new List<FluidValue>();

        await foreach (var item in EnumerateFlattenedAsync(input, context))
        {
            // Like Liquid, an item that can't be indexed (nil, a boolean, a float) makes the filter return nil.
            if (!IsIndexable(item, context))
            {
                return NilValue.Instance;
            }

            var value = await GetPropertyAsync(item, property, context);
            if (seen.Add(value))
            {
                result.Add(item);
            }
        }

        return ArrayValue.Create(result);
    }

    public static async ValueTask<FluidValue> Sum(FluidValue input, FilterArguments arguments, TemplateContext context)
    {
        LiquidException.ThrowFilterArgumentsCount("sum", min: 0, max: null, arguments);

        // Like Liquid, a hash is a one-item array, not a list of entries.
        if (input.Type is FluidValues.Dictionary or FluidValues.Object)
        {
            input = ArrayValue.Create([input]);
        }

        // Like Liquid, a nil property is no property.
        if (arguments.Count == 0 || arguments.At(0).IsNil())
        {
            // Like Liquid, a number or a numeric string is a one-item array.
            if (input is NumberValue or StringValue)
            {
                return NumberValue.Create(input.ToNumberValue(context));
            }

            return NumberValue.Create(await SumItemsAsync(input, context));
        }

        var member = arguments.At(0);

        decimal sum = 0;

        await foreach (var item in EnumerateInputAsync(input, context))
        {
            // Like Liquid, an item that can't be indexed (nil, a boolean, a float) counts as 0, and the property of
            // a string is a substring. An integer has no named property: that's an error.
            if (IsIndexable(item, context))
            {
                sum += await ToSummandAsync(await GetPropertyAsync(item, member, context), context);
            }
        }

        return NumberValue.Create(sum);
    }

    // Like Liquid's to_number: a number or a numeric string, the sum of an array, 0 for anything else.
    private static ValueTask<decimal> ToSummandAsync(FluidValue item, TemplateContext context)
    {
        return item switch
        {
            NumberValue or StringValue => new ValueTask<decimal>(item.ToNumberValue(context)),
            _ when item.Type == FluidValues.Array => SumItemsAsync(item, context),
            _ => new ValueTask<decimal>(0)
        };
    }

    /// <summary>
    /// Sums the numbers and numeric strings of an array, including the items of nested arrays of any kind.
    /// </summary>
    private static async ValueTask<decimal> SumItemsAsync(FluidValue input, TemplateContext context)
    {
        decimal sum = 0;

        await foreach (var item in input.EnumerateAsync(context))
        {
            sum += await ToSummandAsync(item, context);
        }

        return sum;
    }
}

internal sealed class LiquidSortComparer : IComparer<FluidValue>
{
    private readonly bool _caseSensitive;
    private readonly bool _compareNumbersAsStrings;
    private readonly TemplateContext _context;

    public LiquidSortComparer(bool caseSensitive, bool compareNumbersAsStrings, TemplateContext context)
    {
        _caseSensitive = caseSensitive;
        _compareNumbersAsStrings = compareNumbersAsStrings;
        _context = context;
    }

    public int Compare(FluidValue x, FluidValue y)
    {
        // Nil values come last
        if (x.IsNil() && y.IsNil()) return 0;
        if (x.IsNil()) return 1;
        if (y.IsNil()) return -1;

        // If both are numbers and we should compare them numerically
        if (!_compareNumbersAsStrings && x.Type == FluidValues.Number && y.Type == FluidValues.Number)
        {
            var xNum = x.ToNumberValue(_context);
            var yNum = y.ToNumberValue(_context);
            return xNum.CompareTo(yNum);
        }

        // Otherwise, compare as strings
        var xStr = x.ToStringValue(_context);
        var yStr = y.ToStringValue(_context);

        if (_caseSensitive)
        {
            return string.Compare(xStr, yStr, StringComparison.Ordinal);
        }
        else
        {
            return string.Compare(xStr, yStr, StringComparison.OrdinalIgnoreCase);
        }
    }
}
