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

        if (input.Type == FluidValues.Array)
        {
            await foreach (var item in EnumerateFlattenedAsync(input, context))
            {
                context.EnsureCollectionSize((long)list.Count + 1);
                list.Add(await item.GetIndexAsync(member, context));
            }
        }
        else
        {
            context.EnsureCollectionSize(1);
            list.Add(await input.GetIndexAsync(member, context));
        }

        return ArrayValue.Create(list);
    }

    /// <summary>
    /// Reads the property of an item like Liquid's <c>item[property]</c>: on a hash, size, first and last are keys
    /// like any other, missing ones are nil.
    /// </summary>
    internal static ValueTask<FluidValue> GetPropertyAsync(FluidValue item, string property, TemplateContext context)
    {
        return item is DictionaryValue dictionary ? new ValueTask<FluidValue>(dictionary.GetKey(property)) : item.GetValueAsync(property, context);
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
    public static async ValueTask<FluidValue> Where(FluidValue input, FilterArguments arguments, TemplateContext context)
    {
        LiquidException.ThrowFilterArgumentsCount("where", min: 1, max: 2, arguments);

        // Golden Liquid: undefined/nil left values yield an empty array
        if (input.IsNil())
        {
            return ArrayValue.Empty;
        }

        // Like Liquid, a string or a hash is filtered as a one-item array.
        if (input.Type is FluidValues.String or FluidValues.Dictionary or FluidValues.Object)
        {
            input = ArrayValue.Create([input]);
        }

        // Golden Liquid expects where to throw if input is not an array
        if (input.Type != FluidValues.Array)
        {
            throw new LiquidException("where expects an array");
        }

        // First argument is the property name to match
        var member = arguments.At(0);

        // If first argument is nil/undefined, return empty array
        if (member.IsNil())
        {
            return ArrayValue.Empty;
        }

        var memberName = member.ToStringValue(context);

        // Second argument is the value to match
        var targetValue = arguments.At(1);
        var hasExplicitTarget = arguments.Count > 1 && !targetValue.IsNil();

        List<FluidValue> list = null;

        await foreach (var item in input.EnumerateAsync(context))
        {
            var itemValue = await GetPropertyAsync(item, memberName, context);

            var match = false;

            // If no second argument provided, check truthiness of property value
            if (!hasExplicitTarget)
            {
                match = itemValue.ToBooleanValue(context);
            }
            // If second argument is explicitly provided, check equality (including nil)
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

        return list != null ? ArrayValue.Create(list) : ArrayValue.Empty;
    }

    public static async ValueTask<FluidValue> Find(FluidValue input, FilterArguments arguments, TemplateContext context)
    {
        // First argument is the property name or substring to match
        var member = arguments.At(0).ToStringValue(context);

        // Second argument is the value to match (optional)
        var targetValue = arguments.At(1);
        var hasTargetValue = !targetValue.IsNil();

        // Handle string input - check for substring match
        if (input.Type == FluidValues.String)
        {
            var inputString = input.ToStringValue(context);

            if (hasTargetValue)
            {
                // When target value is specified, check if input contains both member and target as substrings
                if (inputString.Contains(member) && inputString.Contains(targetValue.ToStringValue(context)))
                {
                    return input;
                }
            }
            else
            {
                // When no target value, check if input contains the member as substring
                if (inputString.Contains(member))
                {
                    return input;
                }
            }

            return NilValue.Instance;
        }

        // Handle non-array, non-string input (e.g., hash/object)
        if (input.Type != FluidValues.Array)
        {
            // Treat object as single-element array
            var itemValue = await GetPropertyAsync(input, member, context);

            if (hasTargetValue)
            {
                if (targetValue.Equals(itemValue))
                {
                    return input;
                }
            }
            else
            {
                // No target value: check if property exists and is truthy
                if (!itemValue.IsNil() && itemValue.ToBooleanValue(context))
                {
                    return input;
                }
            }

            return NilValue.Instance;
        }

        // Handle array input
        await foreach (var item in input.EnumerateAsync(context))
        {
            // If array contains nil, return nil immediately
            if (item.IsNil())
            {
                return NilValue.Instance;
            }

            if (item.Type == FluidValues.String)
            {
                var itemString = item.ToStringValue(context);

                if (hasTargetValue)
                {
                    // For strings in array with target value, do exact match
                    if (targetValue.Type == FluidValues.String && itemString == targetValue.ToStringValue(context))
                    {
                        return item;
                    }
                }
                else
                {
                    // For strings in array without target value, do substring match
                    if (itemString.Contains(member))
                    {
                        return item;
                    }
                }
            }
            else
            {
                var itemValue = await GetPropertyAsync(item, member, context);

                if (hasTargetValue)
                {
                    if (targetValue.Equals(itemValue))
                    {
                        return item;
                    }
                }
                else
                {
                    // No target value: check if property exists and is truthy
                    if (!itemValue.IsNil() && itemValue.ToBooleanValue(context))
                    {
                        return item;
                    }
                }
            }
        }

        return NilValue.Instance;
    }

    public static async ValueTask<FluidValue> FindIndex(FluidValue input, FilterArguments arguments, TemplateContext context)
    {
        // First argument is the property name or substring to match
        var member = arguments.At(0).ToStringValue(context);

        // Second argument is the value to match (optional)
        var targetValue = arguments.At(1);
        var hasTargetValue = !targetValue.IsNil();

        // Handle string input - check for substring match
        if (input.Type == FluidValues.String)
        {
            var inputString = input.ToStringValue(context);

            if (hasTargetValue)
            {
                // When target value is specified, check if input contains both member and target as substrings
                if (inputString.Contains(member) && inputString.Contains(targetValue.ToStringValue(context)))
                {
                    return NumberValue.Create(0);
                }
            }
            else
            {
                // When no target value, check if input contains the member as substring
                if (inputString.Contains(member))
                {
                    return NumberValue.Create(0);
                }
            }

            return NilValue.Instance;
        }

        // Handle non-array, non-string input (e.g., hash/object)
        if (input.Type != FluidValues.Array)
        {
            // Treat object as single-element array
            var itemValue = await GetPropertyAsync(input, member, context);

            if (hasTargetValue)
            {
                if (targetValue.Equals(itemValue))
                {
                    return NumberValue.Create(0);
                }
            }
            else
            {
                // No target value: check if property exists and is truthy
                if (!itemValue.IsNil() && itemValue.ToBooleanValue(context))
                {
                    return NumberValue.Create(0);
                }
            }

            return NilValue.Instance;
        }

        // Handle array input
        var index = 0;

        await foreach (var item in input.EnumerateAsync(context))
        {
            // If array contains nil, return nil immediately
            if (item.IsNil())
            {
                return NilValue.Instance;
            }

            if (item.Type == FluidValues.String)
            {
                var itemString = item.ToStringValue(context);

                if (hasTargetValue)
                {
                    // For strings in array with target value, do exact match
                    if (targetValue.Type == FluidValues.String && itemString == targetValue.ToStringValue(context))
                    {
                        return NumberValue.Create(index);
                    }
                }
                else
                {
                    // For strings in array without target value, do substring match
                    if (itemString.Contains(member))
                    {
                        return NumberValue.Create(index);
                    }
                }
            }
            else
            {
                var itemValue = await GetPropertyAsync(item, member, context);

                if (hasTargetValue)
                {
                    if (targetValue.Equals(itemValue))
                    {
                        return NumberValue.Create(index);
                    }
                }
                else
                {
                    // No target value: check if property exists and is truthy
                    if (!itemValue.IsNil() && itemValue.ToBooleanValue(context))
                    {
                        return NumberValue.Create(index);
                    }
                }
            }

            index++;
        }

        return NilValue.Instance;
    }

    public static async ValueTask<FluidValue> Has(FluidValue input, FilterArguments arguments, TemplateContext context)
    {
        LiquidException.ThrowFilterArgumentsCount("has", min: 1, max: 2, arguments);

        var firstArg = arguments.At(0);

        // Single argument case
        if (arguments.Count == 1)
        {
            // For arrays: check if value is contained in array
            if (input.Type == FluidValues.Array)
            {
                // When checking array with a string argument
                if (firstArg.Type == FluidValues.String)
                {
                    var searchStr = firstArg.ToStringValue(context);
                    var hasNonString = false;

                    await foreach (var item in input.EnumerateAsync(context))
                    {
                        // Nil items cause error
                        if (item.IsNil())
                        {
                            return NilValue.Instance;
                        }

                        if (item.Type == FluidValues.String)
                        {
                            var itemStr = item.ToStringValue(context);

                            // Check for substring match
                            if (itemStr.Contains(searchStr))
                            {
                                return BooleanValue.True;
                            }
                        }
                        else if (item.Type is FluidValues.Dictionary or FluidValues.Object)
                        {
                            // Like Liquid, a hash matches when the property is truthy.
                            if ((await GetPropertyAsync(item, searchStr, context)).ToBooleanValue(context))
                            {
                                return BooleanValue.True;
                            }
                        }
                        else
                        {
                            hasNonString = true;
                        }
                    }

                    // If array contains non-strings and we're searching with string, throw error
                    if (hasNonString)
                    {
                        throw new LiquidException("has: cannot search for string in non-string array");
                    }

                    return BooleanValue.False;
                }

                // For non-string arguments, check for direct equality
                await foreach (var item in input.EnumerateAsync(context))
                {
                    if (firstArg.Equals(item))
                    {
                        return BooleanValue.True;
                    }
                }
                return BooleanValue.False;
            }
            // For objects/hashes: check if key exists
            else if (input.Type == FluidValues.Object || input.Type == FluidValues.Dictionary)
            {
                var key = firstArg.ToStringValue(context);
                var value = await GetPropertyAsync(input, key, context);
                return value.IsNil() ? BooleanValue.False : BooleanValue.True;
            }
            // For strings: check if substring exists
            else if (input.Type == FluidValues.String)
            {
                var str = input.ToStringValue(context);
                var search = firstArg.ToStringValue(context);
                return str.Contains(search) ? BooleanValue.True : BooleanValue.False;
            }

            return BooleanValue.False;
        }
        // Two arguments case: property name and value to match
        else
        {
            var propertyName = firstArg.ToStringValue(context);
            var targetValue = arguments.At(1);

            // For arrays: check if any item has property with matching value
            if (input.Type == FluidValues.Array)
            {
                await foreach (var item in input.EnumerateAsync(context))
                {
                    // Skip nil items - they should cause the filter to return nil (error)
                    if (item.IsNil())
                    {
                        return NilValue.Instance;
                    }

                    var itemValue = await GetPropertyAsync(item, propertyName, context);

                    if (targetValue.Equals(itemValue))
                    {
                        return BooleanValue.True;
                    }
                }
                return BooleanValue.False;
            }
            // For objects/hashes: check if key exists
            else if (input.Type == FluidValues.Object || input.Type == FluidValues.Dictionary)
            {
                var value = await GetPropertyAsync(input, propertyName, context);

                // When explicitly checking for nil with a hash
                // Return false if key has nil value, true if key exists with non-nil value
                if (targetValue.IsNil())
                {
                    return value.IsNil() ? BooleanValue.False : BooleanValue.True;
                }

                // For non-nil target values, check exact equality
                return targetValue.Equals(value) ? BooleanValue.True : BooleanValue.False;
            }

            return BooleanValue.False;
        }
    }

    public static async ValueTask<FluidValue> Reject(FluidValue input, FilterArguments arguments, TemplateContext context)
    {
        LiquidException.ThrowFilterArgumentsCount("reject", min: 1, max: 2, arguments);

        // Golden Liquid treats strings and hashes as single items, not enumerables
        if (input.Type == FluidValues.String || input.Type == FluidValues.Object || input.Type == FluidValues.Dictionary)
        {
            input = ArrayValue.Create([input]);
        }
        else if (input.Type != FluidValues.Array)
        {
            return input;
        }

        // First argument is the property name to match
        var member = arguments.At(0);
        if (member.IsNil())
        {
            return ArrayValue.Create([]);
        }

        var memberStr = member.ToStringValue(context);

        // Second argument is the value to reject
        var targetValue = arguments.At(1);
        var hasTargetValue = !targetValue.IsNil();

        var list = new List<FluidValue>();
        var hasNilError = false;

        await foreach (var item in input.EnumerateAsync(context))
        {
            // Flatten nested arrays only for Array inputs
            if (item.Type == FluidValues.Array)
            {
                await foreach (var nestedItem in item.EnumerateAsync(context))
                {
                    if (nestedItem.Type == FluidValues.Array)
                    {
                        await foreach (var deepItem in nestedItem.EnumerateAsync(context))
                        {
                            var shouldReject = await ShouldRejectItem(deepItem, memberStr, targetValue, hasTargetValue, context);
                            if (shouldReject.hasError)
                            {
                                if (shouldReject.isNilError)
                                {
                                    hasNilError = true;
                                    break;
                                }
                                throw new LiquidException($"Cannot access property '{memberStr}' on {deepItem.Type}");
                            }
                            if (!shouldReject.reject)
                            {
                                context.EnsureCollectionSize((long)list.Count + 1);
                                list.Add(deepItem);
                            }
                        }
                    }
                    else
                    {
                        var shouldReject = await ShouldRejectItem(nestedItem, memberStr, targetValue, hasTargetValue, context);
                        if (shouldReject.hasError)
                        {
                            if (shouldReject.isNilError)
                            {
                                hasNilError = true;
                                break;
                            }
                            throw new LiquidException($"Cannot access property '{memberStr}' on {nestedItem.Type}");
                        }
                        if (!shouldReject.reject)
                        {
                            context.EnsureCollectionSize((long)list.Count + 1);
                            list.Add(nestedItem);
                        }
                    }
                }
                if (hasNilError) break;
                continue;
            }

            var result = await ShouldRejectItem(item, memberStr, targetValue, hasTargetValue, context);
            if (result.hasError)
            {
                if (result.isNilError)
                {
                    hasNilError = true;
                    break;
                }
                throw new LiquidException($"Cannot access property '{memberStr}' on {item.Type}");
            }

            if (!result.reject)
            {
                context.EnsureCollectionSize((long)list.Count + 1);
                list.Add(item);
            }
        }

        // If we encountered nil error, return empty array
        if (hasNilError)
        {
            return ArrayValue.Create([]);
        }

        return ArrayValue.Create(list);
    }

    private static async ValueTask<(bool reject, bool hasError, bool isNilError)> ShouldRejectItem(
        FluidValue item,
        string memberStr,
        FluidValue targetValue,
        bool hasTargetValue,
        TemplateContext context)
    {
        FluidValue itemValue;

        // Special handling for string items: check substring match
        if (item.Type == FluidValues.String)
        {
            var stringValue = item.ToStringValue(context);
            if (stringValue.Contains(memberStr))
            {
                // Substring found - treat as truthy
                itemValue = BooleanValue.True;
            }
            else
            {
                itemValue = NilValue.Instance;
            }
        }
        else if (item.Type == FluidValues.Nil)
        {
            // Nil doesn't have properties - return nil error (non-throwing)
            return (false, true, true);
        }
        else if (item.Type == FluidValues.Number)
        {
            // Numbers don't have properties, so accessing any property should throw
            return (false, true, false);
        }
        else
        {
            itemValue = await GetPropertyAsync(item, memberStr, context);
        }

        bool reject;
        if (hasTargetValue)
        {
            // Explicit target value: reject if it matches
            reject = targetValue.Equals(itemValue);
        }
        else
        {
            // No target value: reject if property is truthy
            reject = itemValue.ToBooleanValue(context);
        }

        return (reject, false, false);
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

    public static async ValueTask<FluidValue> Sort(FluidValue input, FilterArguments arguments, TemplateContext context)
    {
        LiquidException.ThrowFilterArgumentsCount("sort", min: 0, max: 1, arguments);

        if (input.Type != FluidValues.Array)
        {
            return AsOneItemArray(input);
        }

        // If argument is provided but is nil, treat as no argument
        if (arguments.Count > 0 && !arguments.At(0).IsNil())
        {
            var member = arguments.At(0).ToStringValue(context);

            var values = new List<KeyValuePair<FluidValue, FluidValue>>();

            await foreach (var item in EnumerateFlattenedAsync(input, context))
            {
                values.Add(new KeyValuePair<FluidValue, FluidValue>(item, await GetPropertyAsync(item, member, context)));
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
            return AsOneItemArray(input);
        }

        // If argument is provided but is nil, treat as no argument
        if (arguments.Count > 0 && !arguments.At(0).IsNil())
        {
            var member = arguments.At(0).ToStringValue(context);

            var values = new List<KeyValuePair<FluidValue, FluidValue>>();

            await foreach (var item in EnumerateFlattenedAsync(input, context))
            {
                values.Add(new KeyValuePair<FluidValue, FluidValue>(item, await GetPropertyAsync(item, member, context)));
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

        if (arguments.Count == 0)
        {
            return ArrayValue.Create(await EnumerateFlattenedAsync(input, context).Distinct().ToArrayAsync());
        }

        var property = arguments.At(0).ToStringValue(context);
        var seen = new HashSet<FluidValue>();
        var result = new List<FluidValue>();

        await foreach (var item in EnumerateFlattenedAsync(input, context))
        {
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

        var sumList = new List<decimal>();

        await foreach (var item in EnumerateFlattenedAsync(input, context))
        {
            switch (item)
            {
                case ObjectValue:
                case DictionaryValue:
                    {
                        var value = await GetPropertyAsync(item, member.ToStringValue(context), context);
                        sumList.Add(value.ToNumberValue(context));
                        break;
                    }
                default:
                    // Golden Liquid expects non-hash items to be an error when summing properties
                    throw new LiquidException("sum: when a property is provided, all items must be hashes");
            }
        }

        return NumberValue.Create(sumList.Sum());
    }

    /// <summary>
    /// Sums the numbers and numeric strings of an array, including the items of nested arrays of any kind.
    /// </summary>
    private static async ValueTask<decimal> SumItemsAsync(FluidValue input, TemplateContext context)
    {
        decimal sum = 0;

        await foreach (var item in input.EnumerateAsync(context))
        {
            sum += item switch
            {
                NumberValue or StringValue => item.ToNumberValue(context),
                _ when item.Type == FluidValues.Array => await SumItemsAsync(item, context),
                _ => 0
            };
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
