using System;
using Fluid.Values;

namespace Fluid.Filters
{
    public static class NumberFilters
    {
        public static FilterCollection WithNumberFilters(this FilterCollection filters)
        {
            filters.AddFilter("abs", Abs);
            filters.AddFilter("at_least", AtLeast);
            filters.AddFilter("at_most", AtMost);
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

        public static FluidValue Abs(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            var integral = input is NumberValue numberValue && numberValue.IsIntegral;

            return NumberValue.Create(Math.Abs(input.ToNumberValue()), integral);
        }

        public static FluidValue AtLeast(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            var first = arguments.At(0);

            return input.ToNumberValue() < first.ToNumberValue() ? first : input;
        }

        public static FluidValue AtMost(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            var first = arguments.At(0);

            return input.ToNumberValue() > first.ToNumberValue() ? first : input;
        }

        public static FluidValue Ceil(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            return NumberValue.Create(Math.Ceiling(input.ToNumberValue()), true);
        }

        public static FluidValue DividedBy(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            var first = arguments.At(0);
            double divisor = first.ToNumberValue();

            // The result is rounded down to the nearest integer(that is, the floor) if the divisor is an integer.
            // https://shopify.github.io/liquid/filters/divided_by/

            if (first is NumberValue number)
            {
                if (number.IsIntegral)
                {
                    return NumberValue.Create(Convert.ToInt32(Math.Floor(input.ToNumberValue() / divisor)), true);
                }                
            }

            return NumberValue.Create(input.ToNumberValue() / divisor);
        }

        public static FluidValue Floor(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            return NumberValue.Create(Math.Floor(input.ToNumberValue()), true);
        }

        public static FluidValue Minus(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            return NumberValue.Create(input.ToNumberValue() - arguments.At(0).ToNumberValue());
        }

        public static FluidValue Modulo(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            if (arguments.Count == 0)
            {
                throw new ParseException("The filter 'modulo' requires an argument.");
            }

            return NumberValue.Create(Convert.ToInt32(input.ToNumberValue()) % Convert.ToInt32(arguments.At(0).ToNumberValue()));
        }

        public static FluidValue Plus(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            return NumberValue.Create(input.ToNumberValue() + arguments.At(0).ToNumberValue());
        }

        public static FluidValue Round(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            var digits = Convert.ToInt32(arguments.At(0).Or(NumberValue.Zero).ToNumberValue());
            return NumberValue.Create(Math.Round(input.ToNumberValue(), digits));
        }

        public static FluidValue Times(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            var first = arguments.At(0);
            var resultIsIntegral =
                input is NumberValue inputNumber && inputNumber.IsIntegral
                && first is NumberValue firstNumber && firstNumber.IsIntegral;

            return NumberValue.Create(input.ToNumberValue() * first.ToNumberValue(), resultIsIntegral);
        }
    }
}
