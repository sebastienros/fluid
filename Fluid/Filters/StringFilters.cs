using System;
using System.Collections.Generic;
using System.Linq;
using Fluid.Values;

namespace Fluid.Filters
{
    public static class StringFilters
    {
        public static FiltersCollection WithStringFilters(this FiltersCollection filters)
        {
            filters.Add("append", Append);
            filters.Add("capitalize", Capitalize);
            filters.Add("downcase", Downcase);
            filters.Add("lstrip", LStrip);
            filters.Add("rstrip", RStrip);
            filters.Add("newline_to_br", NewLineToBr);
            filters.Add("prepend", Prepend);
            filters.Add("removefirst", RemoveFirst);
            filters.Add("remove", Remove);
            filters.Add("replacefirst", ReplaceFirst);
            filters.Add("replace", Replace);
            filters.Add("slice", Slice);
            filters.Add("split", Split);
            filters.Add("strip", Strip);
            filters.Add("truncate", Truncate);
            filters.Add("truncatewords", TruncateWords);
            filters.Add("upcase", Upcase);

            return filters;
        }

        public static FluidValue Append(FluidValue input, FluidValue[] arguments, TemplateContext context)
        {
            return new StringValue(input.ToStringValue() + arguments[0].ToStringValue());
        }

        public static char[] CapitalizeDelimiters = new char [] { '-', '.' };

        public static FluidValue Capitalize(FluidValue input, FluidValue[] arguments, TemplateContext context)
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

        public static FluidValue Downcase(FluidValue input, FluidValue[] arguments, TemplateContext context)
        {
            return new StringValue(input.ToStringValue().ToLower());
        }

        public static FluidValue LStrip(FluidValue input, FluidValue[] arguments, TemplateContext context)
        {
            return new StringValue(input.ToStringValue().TrimStart());
        }

        public static FluidValue RStrip(FluidValue input, FluidValue[] arguments, TemplateContext context)
        {
            return new StringValue(input.ToStringValue().TrimEnd());
        }

        public static FluidValue NewLineToBr(FluidValue input, FluidValue[] arguments, TemplateContext context)
        {
            return new StringValue(input.ToStringValue().Replace("\r\n", "<br />").Replace("\n", "<br />"));
        }

        public static FluidValue Prepend(FluidValue input, FluidValue[] arguments, TemplateContext context)
        {
            return new StringValue(arguments[0].ToStringValue() + input.ToStringValue());
        }

        public static FluidValue RemoveFirst(FluidValue input, FluidValue[] arguments, TemplateContext context)
        {
            string remove = arguments[0].ToStringValue();
            var value = input.ToStringValue();

            var index = value.IndexOf(remove);

            if (index != -1)
            {
                return new StringValue(value.Remove(index, remove.Length));
            }

            return input;
        }

        public static FluidValue Remove(FluidValue input, FluidValue[] arguments, TemplateContext context)
        {
            return new StringValue(input.ToStringValue().Replace(arguments[0].ToStringValue(), ""));
        }

        public static FluidValue ReplaceFirst(FluidValue input, FluidValue[] arguments, TemplateContext context)
        {
            string remove = arguments[0].ToStringValue();
            var value = input.ToStringValue();

            var index = value.IndexOf(remove);

            if (index != -1)
            {
                return new StringValue(value.Substring(0, index) + arguments[1].ToStringValue() + value.Substring(index + remove.Length));
            }

            return input;
        }

        public static FluidValue Replace(FluidValue input, FluidValue[] arguments, TemplateContext context)
        {
            return new StringValue(input.ToStringValue().Replace(arguments[0].ToStringValue(), arguments[1].ToStringValue()));
        }

        public static FluidValue Slice(FluidValue input, FluidValue[] arguments, TemplateContext context)
        {
            var source = input.ToStringValue();
            var start = Convert.ToInt32(arguments[0].ToNumberValue());
            var length = Convert.ToInt32(arguments[1].ToNumberValue());

            var len = source.Length;
            var from = start < 0 ? Math.Max(len + start, 0) : Math.Min(start, len);

            return new StringValue(source.Substring(from, length));
        }

        public static FluidValue Split(FluidValue input, FluidValue[] arguments, TemplateContext context)
        {
            return new ArrayValue(input.ToStringValue()
                .Split(new [] { arguments[0].ToStringValue() }, StringSplitOptions.RemoveEmptyEntries)
                .Select(FluidValue.Create)
                .ToArray()
            );
        }

        public static FluidValue Strip(FluidValue input, FluidValue[] arguments, TemplateContext context)
        {
            return new StringValue(input.ToStringValue().Trim());
        }

        public static FluidValue Truncate(FluidValue input, FluidValue[] arguments, TemplateContext context)
        {
            var source = input.ToStringValue().Substring(0, Convert.ToInt32(arguments[0].ToNumberValue()));

            if (arguments.Length > 1)
            {
                source += arguments[1].ToStringValue();
            }

            return new StringValue(source);
        }
        public static FluidValue TruncateWords(FluidValue input, FluidValue[] arguments, TemplateContext context)
        {
            var source = input.ToStringValue();
            var n = Convert.ToInt32(arguments[0].ToNumberValue());

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

            if (arguments.Length > 1)
            {
                source += arguments[1].ToStringValue();
            }

            return new StringValue(source);
        }

        public static FluidValue Upcase(FluidValue input, FluidValue[] arguments, TemplateContext context)
        {
            return new StringValue(input.ToStringValue().ToUpper());
        }

    }
}
