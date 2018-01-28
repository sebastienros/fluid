using System;
using System.Collections.Generic;
using System.Linq;
using Fluid.Values;

namespace Fluid.Filters
{
    public static class StringFilters
    {
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
            filters.AddFilter("truncate", Truncate);
            filters.AddFilter("truncatewords", TruncateWords);
            filters.AddFilter("upcase", Upcase);

            return filters;
        }

        public static FluidValue Append(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            return new StringValue(input.ToStringValue() + arguments.At(0).ToStringValue());
        }

        public static char[] CapitalizeDelimiters = new char [] { '-', '.' };

        public static FluidValue Capitalize(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            var source = input.ToStringValue().ToCharArray();

            for (var i = 0; i < source.Length; i++)
            {
                if (i == 0 || Char.IsWhiteSpace(source[i - 1]) || CapitalizeDelimiters.Contains(source[i - 1]))
                {
                    source[i] = char.ToUpper(source[i]);
                }
            }

            return new StringValue(new String(source));
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
            return new StringValue(input.ToStringValue().Replace(arguments.At(0).ToStringValue(), ""));
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
            var source = input.ToStringValue();
            var start = Convert.ToInt32(arguments.At(0).ToNumberValue());
            var length = Convert.ToInt32(arguments.At(1).ToNumberValue());

            var len = source.Length;
            var from = start < 0 ? Math.Max(len + start, 0) : Math.Min(start, len);

            return new StringValue(source.Substring(from, length));
        }

        public static FluidValue Split(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            return new ArrayValue(input.ToStringValue()
                .Split(new [] { arguments.At(0).ToStringValue() }, StringSplitOptions.RemoveEmptyEntries)
                .Select(FluidValue.Create)
                .ToArray()
            );
        }

        public static FluidValue Strip(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            return new StringValue(input.ToStringValue().Trim());
        }

        public static FluidValue Truncate(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            var ellipsis = "...";
            var text = input.ToStringValue();
            var length = Convert.ToInt32(arguments.At(0).ToNumberValue());

            if (text == null || text.Length <= length)
            {
                return input;
            }
            else
            {
                var source = text.Substring(0, length - ellipsis.Length);

                if (arguments.Count > 1)
                {
                    source += arguments.At(1).ToStringValue();
                }
                source += ellipsis;

                return new StringValue(source);
            }
        }
        public static FluidValue TruncateWords(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            var ellipsis = "...";
            var source = input.ToStringValue();
            var n = Convert.ToInt32(arguments.At(0).ToNumberValue());

            var words = 0;
            for (int i=0; i < source.Length;)
            {
                while(i < source.Length && Char.IsWhiteSpace(source[i])) i++;
                while(i < source.Length && !Char.IsWhiteSpace(source[i])) i++;
                words++;

                if (words == n)
                {
                    source = source.Substring(0, i);   
                    break;
                }
            }

            if (arguments.Count > 1)
            {
                source += arguments.At(1).ToStringValue();
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
