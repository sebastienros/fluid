using Fluid.Values;

namespace Fluid.Filters;

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
        LiquidException.ThrowFilterArgumentsCount("abs", expected: 0, arguments);

        return NumberValue.Create(Math.Abs(input.ToNumberValue(context)));
    }

    public static ValueTask<FluidValue> AtLeast(FluidValue input, FilterArguments arguments, TemplateContext context)
    {
        LiquidException.ThrowFilterArgumentsCount("at_least", expected: 1, arguments);

        var firstValue = arguments.At(0).ToNumberValue(context);
        var inputValue = input.ToNumberValue(context);

        return inputValue < firstValue ? NumberValue.Create(firstValue) : NumberValue.Create(inputValue);
    }

    public static ValueTask<FluidValue> AtMost(FluidValue input, FilterArguments arguments, TemplateContext context)
    {
        LiquidException.ThrowFilterArgumentsCount("at_most", expected: 1, arguments);

        var firstValue = arguments.At(0).ToNumberValue(context);
        var inputValue = input.ToNumberValue(context);

        return inputValue > firstValue ? NumberValue.Create(firstValue) : NumberValue.Create(inputValue);
    }

    public static ValueTask<FluidValue> Ceil(FluidValue input, FilterArguments arguments, TemplateContext context)
    {
        LiquidException.ThrowFilterArgumentsCount("ceil", expected: 0, arguments);

        return NumberValue.Create(decimal.Ceiling(input.ToNumberValue(context)));
    }

    public static ValueTask<FluidValue> DividedBy(FluidValue input, FilterArguments arguments, TemplateContext context)
    {
        LiquidException.ThrowFilterArgumentsCount("divided_by", expected: 1, arguments);

        var first = arguments.At(0);
        decimal divisor = first.ToNumberValue(context);
        decimal dividend = input.ToNumberValue(context);

        // The result is rounded down to the nearest integer(that is, the floor) if BOTH the divisor AND dividend are integers.
        // https://shopify.github.io/liquid/filters/divided_by/

        var result = dividend / divisor;

        var divisorScale = NumberValue.GetScale(divisor);
        var dividendScale = NumberValue.GetScale(dividend);

        if (divisorScale == 0 && dividendScale == 0)
        {
            return NumberValue.Create(decimal.Floor(result));
        }

        // For float division, round to 15 decimal places to match Shopify's implementation
        result = Math.Round(result, 15);

        // Ensure the result has at least scale 1 when doing float division
        // by adding .0 if needed (multiply by 10, divide by 10.0)
        if (NumberValue.GetScale(result) == 0)
        {
            result = result * 1.0m; // This forces at least scale 1
        }

        return NumberValue.Create(result);
    }

    public static ValueTask<FluidValue> Floor(FluidValue input, FilterArguments arguments, TemplateContext context)
    {
        LiquidException.ThrowFilterArgumentsCount("floor", expected: 0, arguments);

        return NumberValue.Create(decimal.Floor(input.ToNumberValue(context)));
    }

    public static ValueTask<FluidValue> Minus(FluidValue input, FilterArguments arguments, TemplateContext context)
    {
        LiquidException.ThrowFilterArgumentsCount("minus", expected: 1, arguments);

        return NumberValue.Create(input.ToNumberValue(context) - arguments.At(0).ToNumberValue(context));
    }

    public static ValueTask<FluidValue> Modulo(FluidValue input, FilterArguments arguments, TemplateContext context)
    {
        LiquidException.ThrowFilterArgumentsCount("modulo", expected: 1, arguments);

        var inputValue = input is NumberValue inputNumber ? inputNumber.Value : input.ToNumberValue(context);
        var divisorValue = arguments.At(0);
        var divisor = divisorValue is NumberValue divisorNumber ? divisorNumber.Value : divisorValue.ToNumberValue(context);
        var result = inputValue % divisor;

        // Liquid uses Ruby's modulo, whose result has the sign of the divisor: -7 % 3 is 2.
        if (result != 0 && (result < 0) != (divisor < 0))
        {
            result += divisor;
        }

        // Preserve decimal format when divisor has decimal places or is from a string with decimal
        // Only a zero result needs it, so the divisor isn't inspected for any other.
        if (result == 0 && HasDecimalPoint(divisorValue, context))
        {
            // Return 0 with one decimal place to match divisor format
            return NumberValue.Create(0.0m);
        }

        return NumberValue.Create(result);
    }

    private static bool HasDecimalPoint(FluidValue value, TemplateContext context)
    {
        // A number is written with a decimal point exactly when it has a scale.
        if (value is NumberValue)
        {
            return NumberValue.GetScale(value.ToNumberValue(context)) != 0;
        }

        return value.ToStringValue(context).Contains('.');
    }

    public static ValueTask<FluidValue> Plus(FluidValue input, FilterArguments arguments, TemplateContext context)
    {
        LiquidException.ThrowFilterArgumentsCount("plus", expected: 1, arguments);

        return NumberValue.Create(input.ToNumberValue(context) + arguments.At(0).ToNumberValue(context));
    }

    public static ValueTask<FluidValue> Round(FluidValue input, FilterArguments arguments, TemplateContext context)
    {
        LiquidException.ThrowFilterArgumentsCount("round", min: 0, max: 1, arguments);

        var digits = Convert.ToInt32(arguments.At(0).Or(NumberValue.Zero).ToNumberValue(context));

        var value = input.ToNumberValue(context);

        // Liquid rounds halves away from zero (2.5 is 3), where Math.Round defaults to the nearest even number.
        if (digits < 0)
        {
            var factor = (decimal)Math.Pow(10, -digits);
            var scaled = value / factor;
            var rounded = Math.Round(scaled, 0, MidpointRounding.AwayFromZero) * factor;
            return NumberValue.Create(rounded);
        }

        return NumberValue.Create(Math.Round(value, digits, MidpointRounding.AwayFromZero));
    }

    public static ValueTask<FluidValue> Times(FluidValue input, FilterArguments arguments, TemplateContext context)
    {
        LiquidException.ThrowFilterArgumentsCount("times", expected: 1, arguments);

        var first = arguments.At(0);

        return NumberValue.Create(input.ToNumberValue(context) * first.ToNumberValue(context));
    }

    public static ValueTask<FluidValue> Random(FluidValue input, FilterArguments arguments, TemplateContext context)
    {
        LiquidException.ThrowFilterArgumentsCount("random", expected: 2, arguments);

        var minValue = (int)arguments.At(0).ToNumberValue(context);
        var maxValue = (int)arguments.At(1).ToNumberValue(context);

        if (minValue > maxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(arguments), "The minimum cannot be greater than the maximum.");
        }

        var random = new Random();
        var range = (long)maxValue - minValue + 1;
        var value = minValue + (long)(random.NextDouble() * range);

        return NumberValue.Create(value);
    }
}
