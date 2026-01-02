using Fluid.Values;

namespace Fluid.Filters
{
    public static class StringFilters
    {
        private const string EllipsisString = "...";
        private static readonly StringValue Ellipsis = new StringValue(EllipsisString);
        private static readonly NumberValue DefaultTruncateLength = NumberValue.Create(50);

        public static FilterCollection WithStringFilters(this FilterCollection filters)
        {
            filters.AddFilter("append", Append);
            filters.AddFilter("capitalize", Capitalize);
            filters.AddFilter("downcase", Downcase);
            filters.AddFilter("lstrip", LStrip);
            filters.AddFilter("rstrip", RStrip);
            filters.AddFilter("newline_to_br", NewLineToBr);
            filters.AddFilter("prepend", Prepend);
            filters.AddFilter("remove_first", RemoveFirst);
            filters.AddFilter("remove", Remove);
            filters.AddFilter("remove_last", RemoveLast);
            filters.AddFilter("replace_first", ReplaceFirst);
            filters.AddFilter("replace", Replace);
            filters.AddFilter("replace_last", ReplaceLast);
            filters.AddFilter("slice", Slice);
            filters.AddFilter("split", Split);
            filters.AddFilter("strip", Strip);
            filters.AddFilter("strip_newlines", StripNewLines);
            filters.AddFilter("truncate", Truncate);
            filters.AddFilter("truncatewords", TruncateWords);
            filters.AddFilter("upcase", Upcase);

            return filters;
        }

        public static ValueTask<FluidValue> Append(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            LiquidException.ThrowFilterArgumentsCount("append", expected: 1, arguments);

            return new StringValue(input.ToStringValue() + arguments.At(0).ToStringValue());
        }

        public static ValueTask<FluidValue> Capitalize(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            LiquidException.ThrowFilterArgumentsCount("capitalize", expected: 0, arguments);

            var source = input.ToStringValue().ToCharArray();

            for (var i = 0; i < source.Length; i++)
            {
                char c;
                if (i == 0 || char.IsWhiteSpace(c = source[i - 1]) || c == '-' || c == '.')
                {
                    source[i] = char.ToUpper(source[i], context.CultureInfo);
                }
            }

            return new StringValue(new string(source));
        }

        public static ValueTask<FluidValue> Downcase(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            LiquidException.ThrowFilterArgumentsCount("downcase", expected: 0, arguments);

            return new StringValue(input.ToStringValue().ToLower(context.CultureInfo));
        }

        public static ValueTask<FluidValue> LStrip(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            LiquidException.ThrowFilterArgumentsCount("lstrip", expected: 0, arguments);

            return new StringValue(input.ToStringValue().TrimStart());
        }

        public static ValueTask<FluidValue> RStrip(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            LiquidException.ThrowFilterArgumentsCount("rstrip", expected: 0, arguments);

            return new StringValue(input.ToStringValue().TrimEnd());
        }

        public static ValueTask<FluidValue> NewLineToBr(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            LiquidException.ThrowFilterArgumentsCount("newline_to_br", expected: 0, arguments);

            // Normalize line endings first, then replace with <br />
            return new StringValue(input.ToStringValue()
                .Replace("\r\n", "\n")      // Windows -> Unix
                .Replace("\r", "\n")         // Mac -> Unix
                .Replace("\n", "<br />\n")); // Unix -> <br /> + newline
        }

        public static ValueTask<FluidValue> Prepend(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            LiquidException.ThrowFilterArgumentsCount("prepend", expected: 1, arguments);

            return new StringValue(arguments.At(0).ToStringValue() + input.ToStringValue());
        }

        public static ValueTask<FluidValue> RemoveFirst(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            LiquidException.ThrowFilterArgumentsCount("remove_first", expected: 1, arguments);

            var remove = arguments.At(0).ToStringValue();
            var value = input.ToStringValue();

            var index = value.IndexOf(remove);

            if (index != -1)
            {
                return new StringValue(value.Remove(index, remove.Length));
            }

            return input;
        }

        public static ValueTask<FluidValue> Remove(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            LiquidException.ThrowFilterArgumentsCount("remove", expected: 1, arguments);

            var argument = arguments.At(0).ToStringValue();

            if (String.IsNullOrEmpty(argument))
            {
                return new StringValue(input.ToStringValue());
            }

            return new StringValue(input.ToStringValue().Replace(argument, ""));
        }

        public static ValueTask<FluidValue> RemoveLast(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            LiquidException.ThrowFilterArgumentsCount("remove_last", expected: 1, arguments);

            var remove = arguments.At(0).ToStringValue();
            var value = input.ToStringValue();

            var index = value.LastIndexOf(remove);

            if (index != -1)
            {
                return new StringValue(value.Remove(index, remove.Length));
            }

            return input;
        }

        public static ValueTask<FluidValue> ReplaceFirst(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            LiquidException.ThrowFilterArgumentsCount("replace_first", expected: 2, arguments);

#if NET6_0_OR_GREATER
            var value = input.ToStringValue().AsSpan();
            var remove = arguments.At(0).ToStringValue().AsSpan();
#else
            var value = input.ToStringValue();
            var remove = arguments.At(0).ToStringValue();
#endif
            var index = value.IndexOf(remove);

            if (index == -1)
            {
                return input;
            }

#if NET6_0_OR_GREATER
            var concat = string.Concat(value.Slice(0, index), arguments.At(1).ToStringValue(), value.Slice(index + remove.Length));
#else
            var concat = string.Concat(value.Substring(0, index), arguments.At(1).ToStringValue(), value.Substring(index + remove.Length));
#endif
            return new StringValue(concat);
        }

        public static ValueTask<FluidValue> Replace(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            LiquidException.ThrowFilterArgumentsCount("replace", min: 1, max: 2, arguments);

            return new StringValue(input.ToStringValue().Replace(arguments.At(0).ToStringValue(), arguments.At(1).ToStringValue()));
        }

        public static ValueTask<FluidValue> ReplaceLast(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            LiquidException.ThrowFilterArgumentsCount("replace_last", expected: 2, arguments);

#if NET6_0_OR_GREATER
            var value = input.ToStringValue().AsSpan();
            var remove = arguments.At(0).ToStringValue().AsSpan();
#else
            var value = input.ToStringValue();
            var remove = arguments.At(0).ToStringValue();
#endif
            var index = value.LastIndexOf(remove);

            if (index == -1)
            {
                return input;
            }

#if NET6_0_OR_GREATER
            var concat = string.Concat(value.Slice(0, index), arguments.At(1).ToStringValue(), value.Slice(index + remove.Length));
#else
            var concat = string.Concat(value.Substring(0, index), arguments.At(1).ToStringValue(), value.Substring(index + remove.Length));
#endif
            return new StringValue(concat);
        }

        public static ValueTask<FluidValue> Slice(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            LiquidException.ThrowFilterArgumentsCount("slice", min: 1, max: 2, arguments);

            var firstArgument = arguments.At(0);

            if (!firstArgument.IsInteger())
            {
                throw new ArgumentException("Slice: offset argument is an invalid number");
            }

            var requestedStartIndex = Convert.ToInt32(firstArgument.ToNumberValue());
            var requestedLength = 1;

            if (arguments.Count > 1)
            {
                var secondArgument = arguments.At(1);
                if (!secondArgument.IsNil() && !secondArgument.IsInteger())
                {
                    throw new ArgumentException("Slice: length argument is not a number");
                }
                requestedLength = secondArgument.IsNil() ? 1 : Convert.ToInt32(secondArgument.ToNumberValue());
            }

            if (input.Type == FluidValues.Array)
            {
                if (requestedLength <= 0)
                {
                    return ArrayValue.Empty;
                }

                var sourceArray = ((ArrayValue)input).Values;

                var sourceLength = sourceArray.Count;

                if (requestedStartIndex < 0 && Math.Abs(requestedStartIndex) > sourceLength)
                {
                    return ArrayValue.Empty;
                }

                var startIndex = requestedStartIndex < 0 ? Math.Max(sourceLength + requestedStartIndex, 0) : Math.Min(requestedStartIndex, sourceLength);
                var length = requestedLength > sourceLength ? sourceLength : requestedLength;
                length = startIndex > 0 && length + startIndex > sourceLength ? length - startIndex : length;

                return new ArrayValue(sourceArray.Skip(startIndex).Take(length).ToArray());
            }
            else
            {
                if (requestedLength <= 0)
                {
                    return BlankValue.Instance;
                }

                var sourceString = input.ToStringValue();

                var sourceStringLength = sourceString.Length;

                if (requestedStartIndex < 0 && Math.Abs(requestedStartIndex) > sourceStringLength)
                {
                    return BlankValue.Instance;
                }

                var startIndex = requestedStartIndex < 0 ? Math.Max(sourceStringLength + requestedStartIndex, 0) : Math.Min(requestedStartIndex, sourceStringLength);
                var length = requestedLength + startIndex > sourceStringLength
                    ? sourceStringLength - startIndex
                    : requestedLength;

                return new StringValue(sourceString.Substring(startIndex, length));
            }
        }

        public static ValueTask<FluidValue> Split(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            LiquidException.ThrowFilterArgumentsCount("split", expected: 1, arguments);

            string[] strings;

            var stringInput = input.ToStringValue();
            var separator = arguments.At(0).ToStringValue();

            if (separator == "")
            {
                strings = new string[stringInput.Length];

                for (var i = 0; i < stringInput.Length; i++)
                {
                    strings[i] = stringInput[i].ToString();
                }
            }
            else
            {
                strings = stringInput.Split(separator, StringSplitOptions.None);
            }

            var values = new FluidValue[strings.Length];
            for (var i = 0; i < strings.Length; i++)
            {
                values[i] = StringValue.Create(strings[i]);
            }

            return new ArrayValue(values);
        }

        public static ValueTask<FluidValue> Strip(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            LiquidException.ThrowFilterArgumentsCount("strip", expected: 0, arguments);

            return new StringValue(input.ToStringValue().Trim());
        }

        public static ValueTask<FluidValue> StripNewLines(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            LiquidException.ThrowFilterArgumentsCount("strip_newlines", expected: 0, arguments);

            var result = input.ToStringValue();

            if (result.Contains('\r'))
            {
                result = result.Replace("\r", "");
            }
            if (result.Contains('\n'))
            {
                result = result.Replace("\n", "");
            }

            return new StringValue(result);
        }

        public static ValueTask<FluidValue> Truncate(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            LiquidException.ThrowFilterArgumentsCount("truncate", min: 0, max: 2, arguments);

            if (input.IsNil())
            {
                return StringValue.Empty;
            }

            var inputStr = input.ToStringValue();

            if (inputStr == null)
            {
                return StringValue.Empty;
            }

            // If first argument is not provided, use default of 50
            // If first argument is nil (undefined variable), throw error per Golden Liquid spec
            var firstArg = arguments.At(0);
            int length;
            if (arguments.Count == 0)
            {
                length = 50; // Default length
            }
            else if (firstArg.IsNil())
            {
                throw new ArgumentException("truncate: cannot convert nil to number");
            }
            else
            {
                length = Convert.ToInt32(firstArg.ToNumberValue());
            }

            if (inputStr.Length <= length)
            {
                return input;
            }

            // If second argument is not provided, use default "..."
            // If second argument is nil (undefined variable), use empty string per Golden Liquid spec
            var secondArg = arguments.At(1);
            string ellipsisStr;
            if (arguments.Count < 2)
            {
                ellipsisStr = "..."; // Default ellipsis when not provided
            }
            else if (secondArg.IsNil())
            {
                ellipsisStr = ""; // Undefined variable means no ellipsis
            }
            else
            {
                ellipsisStr = secondArg.ToStringValue();
            }

            var l = Math.Max(0, length - ellipsisStr.Length);

#if NET6_0_OR_GREATER
            var concat = string.Concat(inputStr.AsSpan().Slice(0, l), ellipsisStr);
#else
            var concat = string.Concat(inputStr.Substring(0, l), ellipsisStr);
#endif
            return new StringValue(concat);
        }

        public static ValueTask<FluidValue> TruncateWords(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            LiquidException.ThrowFilterArgumentsCount("truncate_words", min: 0, max: 2, arguments);

            var source = input.ToStringValue();

            // If first argument is not provided, use default of 15
            // If first argument is nil (undefined variable), throw error per Golden Liquid spec
            var firstArg = arguments.At(0);
            int size;
            if (arguments.Count == 0)
            {
                size = 15; // Default word count
            }
            else if (firstArg.IsNil())
            {
                throw new ArgumentException("truncatewords: cannot convert nil to number");
            }
            else
            {
                size = Convert.ToInt32(firstArg.ToNumberValue());
            }

            if (size <= 0)
            {
                size = 1;
            }

            // If second argument is not provided, use default "..."
            // If second argument is nil (undefined variable), use empty string per Golden Liquid spec
            var secondArg = arguments.At(1);
            string ellipsis;
            if (arguments.Count < 2)
            {
                ellipsis = "..."; // Default ellipsis when not provided
            }
            else if (secondArg.IsNil())
            {
                ellipsis = ""; // Undefined variable means no ellipsis
            }
            else
            {
                ellipsis = secondArg.ToStringValue();
            }

            var chunks = new List<string>();

            var length = source.Length;
            for (var i = 0; i < length && chunks.Count < size;)
            {
                while (i < length && char.IsWhiteSpace(source[i++])) ;
                var start = i - 1;
                while (i < length && !char.IsWhiteSpace(source[i++])) ;
                chunks.Add(source.Substring(start, i - start - (i < length ? 1 : 0)));
            }

            if (chunks.Count >= size)
            {
                chunks[^1] += ellipsis;
            }

            return new StringValue(string.Join(" ", chunks));
        }

        public static ValueTask<FluidValue> Upcase(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            LiquidException.ThrowFilterArgumentsCount("upcase", expected: 0, arguments);

            return new StringValue(input.ToStringValue().ToUpper(context.CultureInfo));
        }
    }
}
