using System;
using System.Collections.Generic;
using System.Linq;
using Fluid.Values;

namespace Fluid.Filters
{
    public static class ArrayFilters
    {
        public static FiltersCollection WithArrayFilters(this FiltersCollection filters)
        {
            filters.Add("join", Join);
            filters.Add("first", First);
            filters.Add("last", Last);
            filters.Add("concat", Concat);
            filters.Add("map", Map);
            filters.Add("reverse", Reverse);
            filters.Add("size", Size);
            filters.Add("sort", Sort);
            filters.Add("uniq", Uniq);

            return filters;
        }

        public static FluidValue Join(FluidValue input, FluidValue[] arguments, TemplateContext context)
        {
            if (input.Type != FluidValues.Array)
            {
                return input;
            }

            return new StringValue(String.Join(arguments[0].ToStringValue(), input.Enumerate().Select(x => x.ToStringValue()).ToArray()));
        }


        public static FluidValue First(FluidValue input, FluidValue[] arguments, TemplateContext context)
        {
            if (input.Type != FluidValues.Array)
            {
                return input;
            }

            return input.Enumerate().First();
        }

        public static FluidValue Last(FluidValue input, FluidValue[] arguments, TemplateContext context)
        {
            if (input.Type != FluidValues.Array)
            {
                return input;
            }

            return input.Enumerate().Last();
        }

        public static FluidValue Concat(FluidValue input, FluidValue[] arguments, TemplateContext context)
        {
            if (input.Type != FluidValues.Array)
            {
                return input;
            }

            if (arguments[0].Type != FluidValues.Array)
            {
                return input;
            }

            var concat = new List<FluidValue>();

            foreach(var item in input.Enumerate())
            {
                concat.Add(item);
            }

            foreach (var item in arguments[0].Enumerate())
            {
                concat.Add(item);
            }

            return new ArrayValue(concat);
        }

        public static FluidValue Map(FluidValue input, FluidValue[] arguments, TemplateContext context)
        {
            if (input.Type != FluidValues.Array)
            {
                return input;
            }

            var member = arguments[0].ToStringValue();

            var list = new List<FluidValue>();

            foreach(var item in input.Enumerate())
            {
                list.Add(item.GetValue(member, context));
            }

            return new ArrayValue(list);
        }

        public static FluidValue Reverse(FluidValue input, FluidValue[] arguments, TemplateContext context)
        {
            if (input.Type != FluidValues.Array)
            {
                return input;
            }

            return new ArrayValue(input.Enumerate().Reverse());
        }

        public static FluidValue Size(FluidValue input, FluidValue[] arguments, TemplateContext context)
        {
            if (input.Type == FluidValues.Array)
            {
                return ((ArrayValue)input).GetValue("size", context);
            }

            if (input.Type == FluidValues.String)
            {
                return ((StringValue)input).GetValue("size", context);
            }

            return NilValue.Instance;
        }

        public static FluidValue Sort(FluidValue input, FluidValue[] arguments, TemplateContext context)
        {
            var member = arguments[0].ToStringValue();

            return new ArrayValue(input.Enumerate().OrderBy(x => x.GetValue(member, context).ToObjectValue()).ToArray());
        }

        public static FluidValue Uniq(FluidValue input, FluidValue[] arguments, TemplateContext context)
        {
            return new ArrayValue(input.Enumerate().Select(x => x.ToObjectValue()).Distinct().ToArray());
        }
    }
}
