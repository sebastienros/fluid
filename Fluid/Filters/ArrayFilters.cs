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
            LiquidException.ThrowFilterArgumentsCount("join", min: 0, max: 1, arguments);

            if (input.Type != FluidValues.Array)
            {
                return input;
            }

            var separator = arguments.Count > 0 ? arguments.At(0).ToStringValue() : " ";
            var values = input.EnumerateAsync(context).Select(x => x.ToStringValue()).Where(x => x != "");
            var joined = string.Join(separator, await values.ToListAsync());
            return new StringValue(joined);
        }

        public static ValueTask<FluidValue> First(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            LiquidException.ThrowFilterArgumentsCount("first", expected: 0, arguments);

            return input.GetValueAsync("first", context);
        }

        public static ValueTask<FluidValue> Last(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            LiquidException.ThrowFilterArgumentsCount("last", expected: 0, arguments);

            return input.GetValueAsync("last", context);
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
                foreach (var item in Flatten(input, context))
                {
                    concat.Add(item);
                }
            }
            else if (input.Type != FluidValues.Nil)
            {
                concat.Add(input);
            }

            if (arg.Type == FluidValues.Array)
            {
                foreach (var item in Flatten(arg, context))
                {
                    concat.Add(item);
                }
            }
            else
            {
                concat.Add(arg);
            }

            return new ArrayValue(concat);

            static IEnumerable<FluidValue> Flatten(FluidValue value, TemplateContext context)
            {
                foreach (var item in value.Enumerate(context))
                {
                    if (value.Type != FluidValues.Array)
                    {
                        yield return value;
                    }
                    else
                    {
                        foreach (var subItem in Flatten(item, context))
                        {
                            yield return subItem;
                        }
                    }
                }
            }
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

            var memberName = member.ToStringValue();

            // Handle objects/hashes: treat them as single-element arrays
            if (input.Type == FluidValues.Object || input.Type == FluidValues.Dictionary)
            {
                var value = await input.GetValueAsync(memberName, context);
                return new ArrayValue([value]);
            }

            // Non-array, non-object inputs should throw an error
            if (input.Type != FluidValues.Array)
            {
                throw new LiquidException("map filter: input must be an array or object");
            }

            var list = new List<FluidValue>();

            await foreach (var item in FlattenForMap(input, context))
            {
                // Each item must be an object or hash to extract properties from
                if (item.Type != FluidValues.Object && item.Type != FluidValues.Dictionary)
                {
                    throw new LiquidException("map filter: all items in the array must be objects");
                }

                list.Add(await item.GetValueAsync(memberName, context));
            }

            return new ArrayValue(list);

            // Flatten nested arrays within the input array for map filter
            static async IAsyncEnumerable<FluidValue> FlattenForMap(FluidValue value, TemplateContext context)
            {
                await foreach (var item in value.EnumerateAsync(context))
                {
                    if (item.Type == FluidValues.Array)
                    {
                        await foreach (var subItem in FlattenForMap(item, context))
                        {
                            yield return subItem;
                        }
                    }
                    else
                    {
                        yield return item;
                    }
                }
            }
        }

        public static async ValueTask<FluidValue> Reverse(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            LiquidException.ThrowFilterArgumentsCount("reverse", expected: 0, arguments);

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
            LiquidException.ThrowFilterArgumentsCount("where", min: 1, max: 2, arguments);

            // If input is not an array, return empty array
            if (input.Type != FluidValues.Array)
            {
                return ArrayValue.Empty;
            }

            // First argument is the property name to match
            var member = arguments.At(0);
            
            // If first argument is nil/undefined, return empty array
            if (member.IsNil())
            {
                return ArrayValue.Empty;
            }
            
            var memberName = member.ToStringValue();

            // Second argument is the value to match
            var targetValue = arguments.At(1);
            var hasExplicitTarget = arguments.Count > 1;

            List<FluidValue> list = null;

            await foreach (var item in input.EnumerateAsync(context))
            {
                var itemValue = await item.GetValueAsync(memberName, context);

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

            return list != null ? new ArrayValue(list) : ArrayValue.Empty;
        }

        public static async ValueTask<FluidValue> Find(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            // First argument is the property name or substring to match
            var member = arguments.At(0).ToStringValue();

            // Second argument is the value to match (optional)
            var targetValue = arguments.At(1);
            var hasTargetValue = !targetValue.IsNil();

            // Handle string input - check for substring match
            if (input.Type == FluidValues.String)
            {
                var inputString = input.ToStringValue();
                
                if (hasTargetValue)
                {
                    // When target value is specified, check if input contains both member and target as substrings
                    if (inputString.Contains(member) && inputString.Contains(targetValue.ToStringValue()))
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
                var itemValue = await input.GetValueAsync(member, context);
                
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
                    var itemString = item.ToStringValue();
                    
                    if (hasTargetValue)
                    {
                        // For strings in array with target value, do exact match
                        if (targetValue.Type == FluidValues.String && itemString == targetValue.ToStringValue())
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
                    var itemValue = await item.GetValueAsync(member, context);

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
            var member = arguments.At(0).ToStringValue();

            // Second argument is the value to match (optional)
            var targetValue = arguments.At(1);
            var hasTargetValue = !targetValue.IsNil();

            // Handle string input - check for substring match
            if (input.Type == FluidValues.String)
            {
                var inputString = input.ToStringValue();
                
                if (hasTargetValue)
                {
                    // When target value is specified, check if input contains both member and target as substrings
                    if (inputString.Contains(member) && inputString.Contains(targetValue.ToStringValue()))
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
                var itemValue = await input.GetValueAsync(member, context);
                
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
                    var itemString = item.ToStringValue();
                    
                    if (hasTargetValue)
                    {
                        // For strings in array with target value, do exact match
                        if (targetValue.Type == FluidValues.String && itemString == targetValue.ToStringValue())
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
                    var itemValue = await item.GetValueAsync(member, context);

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
                        var searchStr = firstArg.ToStringValue();
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
                                var itemStr = item.ToStringValue();
                                
                                // Check for substring match
                                if (itemStr.Contains(searchStr))
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
                    var key = firstArg.ToStringValue();
                    var value = await input.GetValueAsync(key, context);
                    return value.IsNil() ? BooleanValue.False : BooleanValue.True;
                }
                // For strings: check if substring exists
                else if (input.Type == FluidValues.String)
                {
                    var str = input.ToStringValue();
                    var search = firstArg.ToStringValue();
                    return str.Contains(search) ? BooleanValue.True : BooleanValue.False;
                }
                
                return BooleanValue.False;
            }
            // Two arguments case: property name and value to match
            else
            {
                var propertyName = firstArg.ToStringValue();
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

                        var itemValue = await item.GetValueAsync(propertyName, context);
                        
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
                    var value = await input.GetValueAsync(propertyName, context);
                    
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

            // Determine if input is a dictionary (to skip flattening logic for key-value pairs)
            var isDictionary = input.Type == FluidValues.Dictionary;

            // Convert non-array/dictionary inputs to arrays
            if (input.Type == FluidValues.String)
            {
                input = new ArrayValue([input]);
            }
            else if (input.Type != FluidValues.Array && input.Type != FluidValues.Dictionary)
            {
                return input;
            }

            // First argument is the property name to match
            var member = arguments.At(0);
            if (member.IsNil())
            {
                return new ArrayValue([]);
            }
            
            var memberStr = member.ToStringValue();

            // Second argument is the value to reject
            var targetValue = arguments.At(1);
            var hasTargetValue = !targetValue.IsNil();

            var list = new List<FluidValue>();
            var hasNilError = false;

            await foreach (var item in input.EnumerateAsync(context))
            {
                // Flatten nested arrays only for Array inputs, not for Dictionary
                if (!isDictionary && item.Type == FluidValues.Array)
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
                                    throw new InvalidOperationException($"Cannot access property '{memberStr}' on {deepItem.Type}");
                                }
                                if (!shouldReject.reject)
                                {
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
                                throw new InvalidOperationException($"Cannot access property '{memberStr}' on {nestedItem.Type}");
                            }
                            if (!shouldReject.reject)
                            {
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
                    throw new InvalidOperationException($"Cannot access property '{memberStr}' on {item.Type}");
                }
                
                if (!result.reject)
                {
                    list.Add(item);
                }
            }

            // If we encountered nil error, return empty array
            if (hasNilError)
            {
                return new ArrayValue([]);
            }

            return new ArrayValue(list);
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
                var stringValue = item.ToStringValue();
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
                itemValue = await item.GetValueAsync(memberStr, context);
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

            return input.GetValueAsync("size", context);
        }

        public static async ValueTask<FluidValue> Sort(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            LiquidException.ThrowFilterArgumentsCount("sort", min: 0, max: 1, arguments);

            if (input.Type != FluidValues.Array)
            {
                return input;
            }

            // If argument is provided but is nil, treat as no argument
            if (arguments.Count > 0 && !arguments.At(0).IsNil())
            {
                var member = arguments.At(0).ToStringValue();

                var values = new List<KeyValuePair<FluidValue, FluidValue>>();

                await foreach (var item in input.EnumerateAsync(context))
                {
                    values.Add(new KeyValuePair<FluidValue, FluidValue>(item, await item.GetValueAsync(member, context)));
                }

                // Sort with case-sensitive comparison, placing items with missing keys at the end
                var orderedValues = values
                    .OrderBy(x => x.Value.IsNil() ? 1 : 0)
                    .ThenBy(x => x.Value, new LiquidSortComparer(caseSensitive: true, compareNumbersAsStrings: true))
                    .Select(x => x.Key)
                    .ToArray();

                return new ArrayValue(orderedValues);
            }
            else
            {
                var values = await input.EnumerateAsync(context).ToListAsync();
                
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
                    throw new InvalidOperationException("Cannot sort array with incompatible types");
                }
                
                return new ArrayValue(values.OrderBy(x => x, new LiquidSortComparer(caseSensitive: true, compareNumbersAsStrings: false)).ToArray());
            }
        }

        public static async ValueTask<FluidValue> SortNatural(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            LiquidException.ThrowFilterArgumentsCount("sort_natural", min: 0, max: 1, arguments);

            if (input.Type != FluidValues.Array)
            {
                return input;
            }

            // If argument is provided but is nil, treat as no argument
            if (arguments.Count > 0 && !arguments.At(0).IsNil())
            {
                var member = arguments.At(0).ToStringValue();

                var values = new List<KeyValuePair<FluidValue, FluidValue>>();

                await foreach (var item in input.EnumerateAsync(context))
                {
                    values.Add(new KeyValuePair<FluidValue, FluidValue>(item, await item.GetValueAsync(member, context)));
                }

                // Sort with case-insensitive comparison, placing items with missing keys at the end
                var orderedValues = values
                    .OrderBy(x => x.Value.IsNil() ? 1 : 0)
                    .ThenBy(x => x.Value, new LiquidSortComparer(caseSensitive: false, compareNumbersAsStrings: true))
                    .Select(x => x.Key)
                    .ToArray();

                return new ArrayValue(orderedValues);
            }
            else
            {
                var values = await input.EnumerateAsync(context).ToListAsync();
                
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
                    throw new InvalidOperationException("Cannot sort array with incompatible types");
                }
                
                return new ArrayValue(values.OrderBy(x => x, new LiquidSortComparer(caseSensitive: false, compareNumbersAsStrings: false)).ToArray());
            }
        }

        public static async ValueTask<FluidValue> Uniq(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            LiquidException.ThrowFilterArgumentsCount("uniq", min: 0, max: 1, arguments);

            if (input.Type != FluidValues.Array)
            {
                return input;
            }

            if (arguments.Count == 0)
            {
                return new ArrayValue(await input.EnumerateAsync(context).Distinct().ToArrayAsync());
            }

            var property = arguments.At(0).ToStringValue();
            var seen = new HashSet<FluidValue>();
            var result = new List<FluidValue>();

            await foreach (var item in input.EnumerateAsync(context))
            {
                var value = await item.GetValueAsync(property, context);
                if (seen.Add(value))
                {
                    result.Add(item);
                }
            }

            return new ArrayValue(result);
        }

        public static async ValueTask<FluidValue> Sum(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            LiquidException.ThrowFilterArgumentsCount("sum", min: 0, max: null, arguments);

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
                    case DictionaryValue:
                        {
                            var value = await item.GetValueAsync(member.ToStringValue(), context);
                            sumList.Add(value.ToNumberValue());
                            break;
                        }
                    default:
                        // Non-hash items contribute 0 when property argument is provided
                        sumList.Add(0);
                        break;
                }
            }

            return NumberValue.Create(sumList.Sum());
        }
    }

    internal sealed class LiquidSortComparer : IComparer<FluidValue>
    {
        private readonly bool _caseSensitive;
        private readonly bool _compareNumbersAsStrings;

        public LiquidSortComparer(bool caseSensitive, bool compareNumbersAsStrings)
        {
            _caseSensitive = caseSensitive;
            _compareNumbersAsStrings = compareNumbersAsStrings;
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
                var xNum = x.ToNumberValue();
                var yNum = y.ToNumberValue();
                return xNum.CompareTo(yNum);
            }

            // Otherwise, compare as strings
            var xStr = x.ToStringValue();
            var yStr = y.ToStringValue();

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
}
