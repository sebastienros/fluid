using Fluid.Values;
using Fluid.Filters;
using Xunit;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Fluid.Tests;

public class NumberFiltersTests
{

    [Theory]
    [InlineData(4, 4)]
    [InlineData(4.2, 4.2)]
    [InlineData(-4, 4)]
    [InlineData(-4.2, 4.2)]
    public void Abs(decimal value, decimal expectedResult)
    {
        var input = NumberValue.Create(value);

        var arguments = new FilterArguments();
        var context = new TemplateContext();

        var result = NumberFilters.Abs(input, arguments, context);

        Assert.Equal(expectedResult, result.Result.ToNumberValue(context));
    }

    [Theory]
    [InlineData(4, 5, 5)]
    [InlineData(4, 3, 4)]
    [InlineData(-1, 0, 0)]
    public void AtLeast(int value1, object value2, int expectedResult)
    {
        var input = NumberValue.Create(value1);

        var arguments = new FilterArguments(NumberValue.Create(value2, TemplateOptions.Default));
        var context = new TemplateContext();

        var result = NumberFilters.AtLeast(input, arguments, context);

        Assert.Equal(expectedResult, result.Result.ToNumberValue(context));
    }

    [Theory]
    [InlineData(4, 5, 4)]
    [InlineData(4, 3, 3)]
    public void AtMost(int value1, object value2, int expectedResult)
    {
        var input = NumberValue.Create(value1);

        var arguments = new FilterArguments(NumberValue.Create(value2, TemplateOptions.Default));
        var context = new TemplateContext();

        var result = NumberFilters.AtMost(input, arguments, context);

        Assert.Equal(expectedResult, result.Result.ToNumberValue(context));
    }

    [Fact]
    public void Ceil()
    {
        var input = NumberValue.Create(4.6, TemplateOptions.Default);

        var arguments = new FilterArguments();
        var context = new TemplateContext();

        var result = NumberFilters.Ceil(input, arguments, context);

        Assert.Equal(5, result.Result.ToNumberValue(context));
    }

    [Theory]
    [InlineData(4, "5", 0)]
    [InlineData(4, "5.0", 0.8)]
    [InlineData(5, "2", 2)]
    [InlineData(5, "2.0", 2.5)]
    public void DividedByReturnsSameTypeAsDivisor(decimal value, string divisor, decimal expected)
    {
        // https://shopify.github.io/liquid/filters/divided_by/

        var input = NumberValue.Create(value);

        var arguments = new FilterArguments(NumberValue.Create(divisor));
        var context = new TemplateContext();

        var result = NumberFilters.DividedBy(input, arguments, context);

        Assert.Equal(expected, result.Result.ToNumberValue(context));
    }

    [Fact]
    public void Floor()
    {
        var input = NumberValue.Create(4.6, TemplateOptions.Default);

        var arguments = new FilterArguments();
        var context = new TemplateContext();

        var result = NumberFilters.Floor(input, arguments, context);

        Assert.Equal(4, result.Result.ToNumberValue(context));
    }

    [Fact]
    public void Minus()
    {
        var input = NumberValue.Create(6);

        var arguments = new FilterArguments(NumberValue.Create(3));
        var context = new TemplateContext();

        var result = NumberFilters.Minus(input, arguments, context);

        Assert.Equal(3, result.Result.ToNumberValue(context));
    }

    [Theory]
    [InlineData("23.4", "20", 3.4)]
    [InlineData("0.8", ".4", 0.4)]
    [InlineData("0.0003", "0.0001", 0.0002)]
    public void MinusWithDecimalPointFromObject(string input, string argument, decimal expectedResult)
    {
        var inputA = NumberValue.Create(input);
        var inputB = new FilterArguments(StringValue.Create(argument));
        var context = new TemplateContext();

        var result = NumberFilters.Minus(inputA, inputB, context);

        Assert.Equal(expectedResult, result.Result.ToNumberValue(context));
    }

    [Fact]
    public void Modulo()
    {
        var input = NumberValue.Create(6);

        var arguments = new FilterArguments(NumberValue.Create(4));
        var context = new TemplateContext();

        var result = NumberFilters.Modulo(input, arguments, context);

        Assert.Equal(2, result.Result.ToNumberValue(context));
    }

    [Fact]
    public void ModuloWithNoArgumentThrows()
    {
        var input = NumberValue.Create(6);

        var arguments = FilterArguments.Empty;
        var context = new TemplateContext();

        Assert.Throws<LiquidException>(() => NumberFilters.Modulo(input, arguments, context));
    }

    [Fact]
    public void Plus()
    {
        var input = NumberValue.Create(6);

        var arguments = new FilterArguments(StringValue.Create("3"));
        var context = new TemplateContext();

        var result = NumberFilters.Plus(input, arguments, context);

        Assert.Equal(9, result.Result.ToNumberValue(context));
    }

    [Theory]
    [InlineData("23.4", "20", 43.4)]
    [InlineData("0.8", ".4", 1.2)]
    [InlineData("0.0003", "0.0001", 0.0004)]
    public void PlusWithDecimalPointFromObject(string input, string argument, decimal expectedResult)
    {
        var inputA = NumberValue.Create(input);
        var inputB = new FilterArguments(NumberValue.Create(argument));
        var context = new TemplateContext();

        var result = NumberFilters.Plus(inputA, inputB, context);

        Assert.Equal(expectedResult, result.Result.ToNumberValue(context));
    }

    [Fact]
    public void PlusConvertsObjectToNumber()
    {
        var input = ObjectValue.Create("6");

        var arguments = new FilterArguments(NumberValue.Create(3));
        var context = new TemplateContext();

        var result = NumberFilters.Plus(input, arguments, context);

        Assert.Equal(9, result.Result.ToNumberValue(context));
    }

    [Fact]
    public void Round()
    {
        var input = NumberValue.Create((decimal)4.1234);

        var arguments = new FilterArguments(NumberValue.Create(2));
        var context = new TemplateContext();

        var result = NumberFilters.Round(input, arguments, context);

        Assert.Equal(4.12M, result.Result.ToNumberValue(context));
    }

    [Fact]
    public void OperationMaintainsScale()
    {
        var input = NumberValue.Create(6);

        var arguments = new FilterArguments(NumberValue.Create(3));
        var context = new TemplateContext();

        var result = NumberFilters.Times(input, arguments, context);

        Assert.Equal("18", result.Result.ToStringValue(context));
    }

    [Theory]
    [InlineData("23.4", "20", 468)]
    [InlineData("0.8", ".4", 0.32)]
    [InlineData("0.0003", "0.0001", 0.00000003)]
    public void TimesWithDecimalPointFromObject(string input, string argument, decimal expectedResult)
    {
        var inputA = NumberValue.Create(input);
        var inputB = new FilterArguments(NumberValue.Create(argument));
        var context = new TemplateContext();

        var result = NumberFilters.Times(inputA, inputB, context);

        Assert.Equal(expectedResult, result.Result.ToNumberValue(context));
    }

    [Fact]
    public void TimesMaintainsScale()
    {
        var input = NumberValue.Create(6);

        var arguments = new FilterArguments(NumberValue.Create(1.0M));
        var context = new TemplateContext();

        var result = NumberFilters.Times(input, arguments, context);

        Assert.Equal(6, result.Result.ToNumberValue(context));
        Assert.Equal("6.0", result.Result.ToStringValue(context));
    }

    [Fact]
    public void Random_IsNotRegisteredByDefault()
    {
        var options = new TemplateOptionsBuilder().Build();

        Assert.False(options.Filters.TryGetValue("random", out _));
    }

    [Fact]
    public async Task Random_CanBeRegisteredExplicitly()
    {
        var options = new TemplateOptionsBuilder()
            .WithStrictFilters()
            .AddFilter("random", NumberFilters.Random)
            .Build();
        var context = new TemplateContext(options);
        var parser = new FluidParser();
        Assert.True(parser.TryParse("{{ 0 | random: 7, 7 }}", out var template, out var error), error);

        var result = await template.RenderAsync(context);

        Assert.Equal("7", result);
    }

    [Theory]
    [InlineData(1, 10)]
    [InlineData(-10, -1)]
    [InlineData(0, 0)]
    [InlineData(int.MinValue, int.MinValue)]
    [InlineData(int.MaxValue, int.MaxValue)]
    [InlineData(int.MaxValue - 1, int.MaxValue)]
    [InlineData(int.MinValue, int.MaxValue)]
    public void Random(decimal min, decimal max)
    {
        var input = NumberValue.Create(0);

        var arguments = new FilterArguments([NumberValue.Create(min), NumberValue.Create(max)]);
        var context = new TemplateContext();

        for (var i = 0; i < 100; i++)
        {
            var result = NumberFilters.Random(input, arguments, context);

            Assert.InRange(result.Result.ToNumberValue(context), min, max);
        }
    }

    [Theory]
    [InlineData(10, 1)]
    [InlineData(-1, -10)]
    [InlineData(2, 1)]
    [InlineData(-1, -2)]
    [InlineData(int.MaxValue, int.MaxValue - 1)]
    public void Random_ThrowsException_IfMinimumIsGreaterThanMaximum(decimal min, decimal max)
    {
        var input = NumberValue.Create(0);

        var arguments = new FilterArguments([NumberValue.Create(min), NumberValue.Create(max)]);
        var context = new TemplateContext();

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => NumberFilters.Random(input, arguments, context));

        Assert.Equal("arguments", exception.ParamName);
    }

    [Theory]
    // Like Liquid, a decimal string is parsed as is and anything else is read from its leading digits, like Ruby's String#to_i.
    [InlineData("{{ '12abc' | plus: 0 }}|{{ '1-' | plus: 0 }}|{{ '1e2' | plus: 0 }}|{{ '3.5kg' | times: 2 }}|{{ '-' | plus: 0 }}", "12|1|1|6|0")]
    [InlineData("{{ ' 7 ' | plus: 1 }}|{{ '+5' | plus: 0 }}|{{ '1_000' | plus: 0 }}|{{ '1__0' | plus: 0 }}|{{ '_1' | plus: 0 }}", "8|5|1000|1|0")]
    [InlineData("{{ ' 2.5 ' | plus: 1 }}|{{ '-1.5' | plus: 0 }}|{{ '+1.5' | plus: 0 }}|{{ '1.5.2' | plus: 0 }}|{{ '2.50' | plus: 0 }}", "3.5|-1.5|1|1|2.5")]
    [InlineData("{{ 'abc' | plus: 0 }}|{{ '' | plus: 0 }}|{{ '0x10' | plus: 0 }}|{{ '$5' | plus: 0 }}|{{ '1,000' | plus: 0 }}", "0|0|0|0|1")]
    [InlineData("{{ '3:' | ceil }}|{{ '12abc' | at_least: 5 }}|{{ '99999999999999999999999999999999' | plus: 0 }}", "3|12|79228162514264337593543950335")]
    // Like Liquid, values that aren't numbers or strings are 0.
    [InlineData("{{ a | plus: 0 }}|{{ h | times: 1 }}|{{ (1..5) | plus: 0 }}|{{ true | plus: 0 }}|{{ false | plus: 1 }}", "0|0|0|0|1")]
    [InlineData("{{ 1 | plus: a }}|{{ 1 | at_least: a }}|{{ a | ceil }}", "1|1|0")]
    public async Task ConvertsValuesToNumbersLikeLiquid(string source, string expected)
    {
        var context = new TemplateContext();
        context.SetValue("a", new[] { 1, 2, 3 });
        context.SetValue("h", new Dictionary<string, object> { ["k"] = 1 });

        Assert.Equal(expected, await new FluidParser().Parse(source).RenderAsync(context));
    }

    [Theory]
    // Like Liquid (Ruby floats), numbers smaller than 0.0001 are rendered in scientific notation.
    [InlineData("{{ 0.000001 }}|{{ 0.00009 }}|{{ 0.0001 }}|{{ 1.0 | divided_by: 100000 }}|{{ -0.0000025 | plus: 0 }}|{{ 0.00001234 }}", "1.0e-06|9.0e-05|0.0001|1.0e-05|-2.5e-06|1.234e-05")]
    // Filters see the same text as the output: no trailing zeros past the first.
    [InlineData("{{ 0.000001 | append: '' }}|{{ 1.50 | append: '' }}|{{ 1.50 }}|{{ 2 | times: 1.50 }}|{{ 0 | times: 3.19 }}|{{ 3.999 | round: 2 | append: '' }}", "1.0e-06|1.5|1.5|3.0|0.0|4.0")]
    // Like Liquid, a decimal operand makes modulo return a decimal.
    [InlineData("{{ 1 | modulo: 1.5 }}|{{ 3 | modulo: 2.0 }}|{{ 3.0 | modulo: 2 }}|{{ 7 | modulo: 2 }}|{{ 0 | modulo: 1.5 }}|{{ '7.0' | modulo: 2 }}", "1.0|1.0|1.0|1|0.0|1.0")]
    public async Task FormatsNumbersLikeLiquid(string source, string expected)
    {
        Assert.Equal(expected, await new FluidParser().Parse(source).RenderAsync(new TemplateContext()));
    }
}
