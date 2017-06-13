using System.Linq;
using Fluid.Values;
using Fluid.Filters;
using Xunit;

namespace Fluid.Tests
{
    public class NumberFiltersTests
    {
        [Fact]
        public void Ceil()
        {
            var input = new NumberValue(4.6);

            var arguments = new FilterArguments();
            var context = new TemplateContext();

            var result = NumberFilters.Ceil(input, arguments, context);

            Assert.Equal(5, result.ToNumberValue());
        }

        [Fact]
        public void DividedBy()
        {
            var input = new NumberValue(6);

            var arguments = new FilterArguments(3);
            var context = new TemplateContext();

            var result = NumberFilters.DividedBy(input, arguments, context);

            Assert.Equal(2, result.ToNumberValue());
        }

        [Fact]
        public void Floor()
        {
            var input = new NumberValue(4.6);

            var arguments = new FilterArguments();
            var context = new TemplateContext();

            var result = NumberFilters.Floor(input, arguments, context);

            Assert.Equal(4, result.ToNumberValue());
        }

        [Fact]
        public void Minus()
        {
            var input = new NumberValue(6);

            var arguments = new FilterArguments(3);
            var context = new TemplateContext();

            var result = NumberFilters.Minus(input, arguments, context);

            Assert.Equal(3, result.ToNumberValue());
        }

        [Fact]
        public void Modulo()
        {
            var input = new NumberValue(6);

            var arguments = new FilterArguments(4);
            var context = new TemplateContext();

            var result = NumberFilters.Modulo(input, arguments, context);

            Assert.Equal(2, result.ToNumberValue());
        }

        [Fact]
        public void Plus()
        {
            var input = new NumberValue(6);

            var arguments = new FilterArguments('3');
            var context = new TemplateContext();

            var result = NumberFilters.Plus(input, arguments, context);

            Assert.Equal(9, result.ToNumberValue());
        }

        [Fact]
        public void Round()
        {
            var input = new NumberValue(4.1234);

            var arguments = new FilterArguments(2);
            var context = new TemplateContext();

            var result = NumberFilters.Round(input, arguments, context);

            Assert.Equal(4.12, result.ToNumberValue());
        }

        [Fact]
        public void Times()
        {
            var input = new NumberValue(6);

            var arguments = new FilterArguments(3);
            var context = new TemplateContext();

            var result = NumberFilters.Times(input, arguments, context);

            Assert.Equal(18, result.ToNumberValue());
        }
    }
}
