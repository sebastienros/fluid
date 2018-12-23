using System;
using Fluid.Values;

namespace Fluid.Filters
{
    public static class NumberFilters
    {
        public static FilterCollection WithNumberFilters(this FilterCollection filters)
        {
            filters.AddFilter("ceil", Ceil);
            filters.AddFilter("divided_by", DividedBy);
            filters.AddFilter("floor", Floor);
            filters.AddFilter("minus", Minus);
            filters.AddFilter("modulo", Modulo);
            filters.AddFilter("plus", Plus);
            filters.AddFilter("round", Round);
            filters.AddFilter("times", Times);

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
