﻿using System.Linq;
using Fluid.Values;
using Fluid.Filters;
using Xunit;
using System;

namespace Fluid.Tests
{
    public class NumberFiltersTests
    {
        [Fact]
        public void Ceil()
        {
            var input = NumberValue.Create(4.6);

            var arguments = new FilterArguments();
            var context = new TemplateContext();

            var result = NumberFilters.Ceil(input, arguments, context);

            Assert.Equal(5, result.ToNumberValue());
        }

        [Theory]
        [InlineData(4, 5, 0)]
        [InlineData(4, 5.0, 0.8)]
        public void DividedByReturnsSameTypeAsDivisor(double value, object divisor, double expected)
        {
            var input = NumberValue.Create(value);

            var arguments = new FilterArguments(NumberValue.Create(Convert.ToDouble(divisor), divisor is int));
            var context = new TemplateContext();

            var result = NumberFilters.DividedBy(input, arguments, context);

            Assert.Equal(expected, result.ToNumberValue());
        }

        [Fact]
        public void Floor()
        {
            var input = NumberValue.Create(4.6);

            var arguments = new FilterArguments();
            var context = new TemplateContext();

            var result = NumberFilters.Floor(input, arguments, context);

            Assert.Equal(4, result.ToNumberValue());
        }

        [Fact]
        public void Minus()
        {
            var input = NumberValue.Create(6);

            var arguments = new FilterArguments(3);
            var context = new TemplateContext();

            var result = NumberFilters.Minus(input, arguments, context);

            Assert.Equal(3, result.ToNumberValue());
        }

        [Fact]
        public void Modulo()
        {
            var input = NumberValue.Create(6);

            var arguments = new FilterArguments(4);
            var context = new TemplateContext();

            var result = NumberFilters.Modulo(input, arguments, context);

            Assert.Equal(2, result.ToNumberValue());
        }

        [Fact]
        public void Plus()
        {
            var input = NumberValue.Create(6);

            var arguments = new FilterArguments('3');
            var context = new TemplateContext();

            var result = NumberFilters.Plus(input, arguments, context);

            Assert.Equal(9, result.ToNumberValue());
        }

        [Fact]
        public void PlusConvertsObjectToNumber()
        {   
            var input = new ObjectValue("6");

            var arguments = new FilterArguments(3);
            var context = new TemplateContext();

            var result = NumberFilters.Plus(input, arguments, context);

            Assert.Equal(9, result.ToNumberValue());
        }

        [Fact]
        public void Round()
        {
            var input = NumberValue.Create(4.1234);

            var arguments = new FilterArguments(2);
            var context = new TemplateContext();

            var result = NumberFilters.Round(input, arguments, context);

            Assert.Equal(4.12, result.ToNumberValue());
        }

        [Fact]
        public void Times()
        {
            var input = NumberValue.Create(6, true);

            var arguments = new FilterArguments(3);
            var context = new TemplateContext();

            var result = NumberFilters.Times(input, arguments, context);

            Assert.Equal(18, result.ToNumberValue());
            Assert.True(((NumberValue)result).IsIntegral);
        }

        [Fact]
        public void TimesCanChangeToFloat()
        {
            var input = NumberValue.Create(6, true);

            var arguments = new FilterArguments(1.0);
            var context = new TemplateContext();

            var result = NumberFilters.Times(input, arguments, context);

            Assert.Equal(6, result.ToNumberValue());
            Assert.False(((NumberValue)result).IsIntegral);
        }
    }
}
