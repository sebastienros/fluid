using System;
using Fluid.Values;

namespace Fluid.Filters
{
    public static class StringFilters
    {
        private static readonly StringValue Ellipsis = new StringValue("...");

        public static FilterCollection WithStringFilters(this FilterCollection filters)
        {
            filters.AddFilter("append", Append);
            filters.AddFilter("capitalize", Capitalize);
            filters.AddFilter("downcase", Downcase);
            filters.AddFilter("lstrip", LStrip);
            filters.AddFilter("rstrip", RStrip);
            filters.AddFilter("newline_to_br", NewLineToBr);
            filters.AddFilter("prepend", Prepend);
            filters.AddFilter("removefirst", RemoveFirst);
            filters.AddFilter("remove", Remove);
            filters.AddFilter("replacefirst", ReplaceFirst);
            filters.AddFilter("replace", Replace);
            filters.AddFilter("slice", Slice);
            filters.AddFilter("split", Split);
            filters.AddFilter("strip", Strip);
            filters.AddFilter("strip_newlines", StripNewLines);
            filters.AddFilter("truncate", Truncate);
            filters.AddFilter("truncatewords", TruncateWords);
            filters.AddFilter("upcase", Upcase);

            return filters;
        }

        public static FluidValue Append(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            return new StringValue(input.ToStringValue() + arguments.At(0).ToStringValue());
        }

        public static FluidValue Capitalize(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            var source = input.ToStringValue().ToCharArray();

            for (var i = 0; i < source.Length; i++)
            {
                char c;
                if (i == 0 || char.IsWhiteSpace(c = source[i - 1]) || c == '-' || c == '.')
                {
                    source[i] = char.ToUpper(source[i]);
                }
            }

            return new StringValue(new string(source));
        }

        public static FluidValue Downcase(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            return new StringValue(input.ToStringValue().ToLower());
        }

        public static FluidValue LStrip(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            return new StringValue(input.ToStringValue().TrimStart());
        }

        public static FluidValue RStrip(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            return new StringValue(input.ToStringValue().TrimEnd());
        }

        public static FluidValue NewLineToBr(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            return new StringValue(input.ToStringValue().Replace("\r\n", "<br />").Replace("\n", "<br />"));
        }

        public static FluidValue Prepend(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            return new StringValue(arguments.At(0).ToStringValue() + input.ToStringValue());
        }

        public static FluidValue RemoveFirst(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            string remove = arguments.At(0).ToStringValue();
            var value = input.ToStringValue();

            var index = value.IndexOf(remove);

            if (index != -1)
            {
                return new StringValue(value.Remove(index, remove.Length));
            }

            return input;
        }

        public static FluidValue Remove(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            var argument = arguments.At(0).ToStringValue();

            if (String.IsNullOrEmpty(argument))
            {
                return new StringValue(input.ToStringValue());
            }

            return new StringValue(input.ToStringValue().Replace(argument, ""));
        }

        public static FluidValue ReplaceFirst(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            string remove = arguments.At(0).ToStringValue();
            var value = input.ToStringValue();

            var index = value.IndexOf(remove);

            if (index != -1)
            {
                return new StringValue(value.Substring(0, index) + arguments.At(1).ToStringValue() + value.Substring(index + remove.Length));
            }

            return input;
        }

        public static FluidValue Replace(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            return new StringValue(input.ToStringValue().Replace(arguments.At(0).ToStringValue(), arguments.At(1).ToStringValue()));
        }

        public static FluidValue Slice(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            var firstArgument = arguments.At(0);
            var secondArgument = arguments.At(1);

            if (!firstArgument.IsInteger())
            {
                throw new ArgumentException("Slice: offset argument is an invalid number");
            }

            if (arguments.Count > 1 && !secondArgument.IsInteger())
            {
                throw new ArgumentException("Slice: length argument is not a number");
            }

            var requestedStartIndex = Convert.ToInt32(firstArgument.ToNumberValue());
            var requestedLength = Convert.ToInt32(secondArgument.Or(NumberValue.Create(1)).ToNumberValue());

            if (input.Type == FluidValues.Array)
            {
                if (requestedLength <= 0)
                {
                    return ArrayValue.Empty;
                }

                var sourceArray = ((ArrayValue) input).Values;

                var sourceLength = sourceArray.Length;

                if (requestedStartIndex < 0 && Math.Abs(requestedStartIndex) > sourceLength)
                {
                    return ArrayValue.Empty;
                }

                var startIndex = requestedStartIndex < 0 ? Math.Max(sourceLength + requestedStartIndex, 0) : Math.Min(requestedStartIndex, sourceLength);
                var length = requestedLength > sourceLength ? sourceLength : requestedLength;
                length = startIndex > 0 && length + startIndex > sourceLength ? length - startIndex : length;

                var result = new FluidValue[length];

                Array.Copy(sourceArray, startIndex, result, 0, length);

                return new ArrayValue(result);
            }
            else
            {
                if (requestedLength <= 0)
                {
                    return StringValue.Empty;
                }

                var sourceString = input.ToStringValue();
    
                var sourceStringLength = sourceString.Length;
    
                if (requestedStartIndex < 0 && Math.Abs(requestedStartIndex) > sourceStringLength)
                {
                    return StringValue.Empty;
                }

                var startIndex = requestedStartIndex < 0 ? Math.Max(sourceStringLength + requestedStartIndex, 0) : Math.Min(requestedStartIndex, sourceStringLength);
                var length = requestedLength > sourceStringLength ? sourceStringLength : requestedLength;
                length = startIndex > 0 && length + startIndex > sourceStringLength ? length - startIndex : length;

                return new StringValue(sourceString.Substring(startIndex, length));
            }
        }

        public static FluidValue Split(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
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
                strings = stringInput.Split(new[] { separator }, StringSplitOptions.RemoveEmptyEntries);
            }

            var values = new FluidValue[strings.Length];
            for (var i = 0; i < strings.Length; i++)
            {
                values[i] = FluidValue.Create(strings[i]);
            }

            return new ArrayValue(values);
        }

        public static FluidValue Strip(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            return new StringValue(input.ToStringValue().Trim());
        }

        public static FluidValue StripNewLines(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            var result = input.ToStringValue();

            if (result.Contains("\r"))
            {
                result = result.Replace("\r", "");
            }
            if (result.Contains("\n"))
            {
                result = result.Replace("\n", "");
            }

            return new StringValue(result);
        }

        public static FluidValue Truncate(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            var text = input.ToStringValue();
            var size = Math.Max(0, (int)arguments.At(0).ToNumberValue());
            var ellipsis = arguments.At(1).Or(Ellipsis).ToStringValue();

            if (text == null)
            {
                return NilValue.Empty;
            }
            else if (ellipsis.Length >= size)
            {
                return new StringValue(ellipsis);
            }
            else if (text.Length > size - ellipsis.Length)
            {
                // PERF: using StringBuilder/StringBuilderPool is slower
                var source = text.Substring(0, size - ellipsis.Length) + ellipsis;
                return new StringValue(source);
            }
            else
            {
                // PERF: using StringBuilder/StringBuilderPool is slower
                return new StringValue(text + ellipsis);
            }
        }
        public static FluidValue TruncateWords(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            var source = input.ToStringValue();
            var size = Math.Max(0, Convert.ToInt32(arguments.At(0).ToNumberValue()));
            var ellipsis = arguments.At(1).Or(Ellipsis).ToStringValue();

            var words = 0;

            if (size > 0)
            {
                for (var i = 0; i < source.Length;)
                {
                    while (i < source.Length && char.IsWhiteSpace(source[i])) i++;
                    while (i < source.Length && !char.IsWhiteSpace(source[i])) i++;
                    words++;

                    if (words >= size)
                    {
                        source = source.Substring(0, i);
                        break;
                    }
                }
            }
            else
            {
                source = "";
            }

            source += ellipsis;

            return new StringValue(source);
        }

        public static FluidValue Upcase(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            return new StringValue(input.ToStringValue().ToUpper());
        }
    }
}
