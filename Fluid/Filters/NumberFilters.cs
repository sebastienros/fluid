using System;
using System.Collections.Generic;
using System.Text;
using Fluid.Values;

namespace Fluid.Filters
{
    public static class NumberFilters
    {
        public static FiltersCollection WithNumberFilters(this FiltersCollection filters)
        {
            filters.Add("ceil", Ceil);
            filters.Add("divided_by", DividedBy);
            filters.Add("floor", Floor);
            filters.Add("minus", Minus);
            filters.Add("modulo", Modulo);
            filters.Add("plus", Plus);
            filters.Add("round", Round);
            filters.Add("times", Times);

            return filters;
        }

        public static FluidValue Ceil(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            return new NumberValue(Math.Ceiling(input.ToNumberValue()));
        }

        public static FluidValue DividedBy(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            return new NumberValue((int) input.ToNumberValue() / Convert.ToInt32(arguments.At(0).ToNumberValue()));
        }

        public static FluidValue Floor(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            return new NumberValue(Math.Floor(input.ToNumberValue()));
        }

        public static FluidValue Minus(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            return new NumberValue(input.ToNumberValue() - arguments.At(0).ToNumberValue());
        }

        public static FluidValue Modulo(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            return new NumberValue(Convert.ToInt32(input.ToNumberValue()) % Convert.ToInt32(arguments.At(0).ToNumberValue()));
        }

        public static FluidValue Plus(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            return new NumberValue(input.ToNumberValue() + arguments.At(0).ToNumberValue());
        }

        public static FluidValue Round(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            var digits = Convert.ToInt32(arguments.At(0).Or(new NumberValue(0)).ToNumberValue());
            return new NumberValue(Math.Round(input.ToNumberValue(), digits));
        }

        public static FluidValue Times(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            return new NumberValue(input.ToNumberValue() * arguments.At(0).ToNumberValue());
        }
    }
}
