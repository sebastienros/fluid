using System;
using System.Threading.Tasks;
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

        public static ValueTask<FluidValue> Abs(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            return NumberValue.Create(Math.Abs(input.ToNumberValue()));
        }

        public static ValueTask<FluidValue> AtLeast(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            var first = arguments.At(0);

            return input.ToNumberValue() < first.ToNumberValue() ? first : input;
        }

        public static ValueTask<FluidValue> AtMost(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            var first = arguments.At(0);

            return input.ToNumberValue() > first.ToNumberValue() ? first : input;
        }

        public static ValueTask<FluidValue> Ceil(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            return NumberValue.Create(decimal.Ceiling(input.ToNumberValue()));
        }

        public static ValueTask<FluidValue> DividedBy(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            var first = arguments.At(0);
            decimal divisor = first.ToNumberValue();

            // The result is rounded down to the nearest integer(that is, the floor) if the divisor is an integer.
            // https://shopify.github.io/liquid/filters/divided_by/

            var result = input.ToNumberValue() / divisor;

            if (NumberValue.GetScale(divisor) == 0)
            {
                return NumberValue.Create(decimal.Floor(result));
            }

            return NumberValue.Create(result);
        }

        public static ValueTask<FluidValue> Floor(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            return NumberValue.Create(decimal.Floor(input.ToNumberValue()));
        }

        public static ValueTask<FluidValue> Minus(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            return NumberValue.Create(input.ToNumberValue() - arguments.At(0).ToNumberValue());
        }

        public static ValueTask<FluidValue> Modulo(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            if (arguments.Count == 0)
            {
                throw new ParseException("The filter 'modulo' requires an argument.");
            }

            return NumberValue.Create(Convert.ToInt32(input.ToNumberValue()) % Convert.ToInt32(arguments.At(0).ToNumberValue()));
        }

        public static ValueTask<FluidValue> Plus(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            return NumberValue.Create(input.ToNumberValue() + arguments.At(0).ToNumberValue());
        }

        public static ValueTask<FluidValue> Round(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            var digits = Convert.ToInt32(arguments.At(0).Or(NumberValue.Zero).ToNumberValue());
            return NumberValue.Create(Math.Round(input.ToNumberValue(), digits));
        }

        public static ValueTask<FluidValue> Times(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            var first = arguments.At(0);

            return NumberValue.Create(input.ToNumberValue() * first.ToNumberValue());
        }
    }
}
