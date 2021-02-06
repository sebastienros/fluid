using System.Linq;
using Fluid.Values;
using Fluid.Filters;
using Xunit;
using System;

namespace Fluid.Tests
{
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

            Assert.Equal(expectedResult, result.Result.ToNumberValue());
        }

        [Theory]
        [InlineData(4, 5, 5)]
        [InlineData(4, 3, 4)]
        public void AtLeast(int value1, object value2, int expectedResult)
        {
            var input = NumberValue.Create(value1);

            var arguments = new FilterArguments(NumberValue.Create(value2, TemplateOptions.Default));
            var context = new TemplateContext();

            var result = NumberFilters.AtLeast(input, arguments, context);

            Assert.Equal(expectedResult, result.Result.ToNumberValue());
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

            Assert.Equal(expectedResult, result.Result.ToNumberValue());
        }

        [Fact]
        public void Ceil()
        {
            var input = NumberValue.Create(4.6, TemplateOptions.Default);

            var arguments = new FilterArguments();
            var context = new TemplateContext();

            var result = NumberFilters.Ceil(input, arguments, context);

            Assert.Equal(5, result.Result.ToNumberValue());
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

            Assert.Equal(expected, result.Result.ToNumberValue());
        }

        [Fact]
        public void Floor()
        {
            var input = NumberValue.Create(4.6, TemplateOptions.Default);

            var arguments = new FilterArguments();
            var context = new TemplateContext();

            var result = NumberFilters.Floor(input, arguments, context);

            Assert.Equal(4, result.Result.ToNumberValue());
        }

        [Fact]
        public void Minus()
        {
            var input = NumberValue.Create(6);

            var arguments = new FilterArguments(NumberValue.Create(3));
            var context = new TemplateContext();

            var result = NumberFilters.Minus(input, arguments, context);

            Assert.Equal(3, result.Result.ToNumberValue());
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

            Assert.Equal(expectedResult, result.Result.ToNumberValue());
        }

        [Fact]
        public void Modulo()
        {
            var input = NumberValue.Create(6);

            var arguments = new FilterArguments(NumberValue.Create(4));
            var context = new TemplateContext();

            var result = NumberFilters.Modulo(input, arguments, context);

            Assert.Equal(2, result.Result.ToNumberValue());
        }

        [Fact]
        public void ModuloWithNoArgumentThrows()
        {
            var input = NumberValue.Create(6);

            var arguments = FilterArguments.Empty;
            var context = new TemplateContext();

            Assert.Throws<ParseException>(() => NumberFilters.Modulo(input, arguments, context));
        }

        [Fact]
        public void Plus()
        {
            var input = NumberValue.Create(6);

            var arguments = new FilterArguments(StringValue.Create("3"));
            var context = new TemplateContext();

            var result = NumberFilters.Plus(input, arguments, context);

            Assert.Equal(9, result.Result.ToNumberValue());
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

            Assert.Equal(expectedResult, result.Result.ToNumberValue());
        }

        [Fact]
        public void PlusConvertsObjectToNumber()
        {
            var input = new ObjectValue("6");

            var arguments = new FilterArguments(NumberValue.Create(3));
            var context = new TemplateContext();

            var result = NumberFilters.Plus(input, arguments, context);

            Assert.Equal(9, result.Result.ToNumberValue());
        }

        [Fact]
        public void Round()
        {
            var input = NumberValue.Create((decimal) 4.1234);

            var arguments = new FilterArguments(NumberValue.Create(2));
            var context = new TemplateContext();

            var result = NumberFilters.Round(input, arguments, context);

            Assert.Equal(4.12M, result.Result.ToNumberValue());
        }

        [Fact]
        public void OperationMaintainsScale()
        {
            var input = NumberValue.Create(6);

            var arguments = new FilterArguments(NumberValue.Create(3));
            var context = new TemplateContext();

            var result = NumberFilters.Times(input, arguments, context);

            Assert.Equal("18", result.Result.ToStringValue());
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

            Assert.Equal(expectedResult, result.Result.ToNumberValue());
        }

        [Fact]
        public void TimesMaintainsScale()
        {
            var input = NumberValue.Create(6);

            var arguments = new FilterArguments(NumberValue.Create(1.0M));
            var context = new TemplateContext();

            var result = NumberFilters.Times(input, arguments, context);

            Assert.Equal(6, result.Result.ToNumberValue());
            Assert.Equal("6.0", result.Result.ToStringValue());
        }
    }
}
