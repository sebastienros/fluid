using Fluid.Ast;
using Fluid.Filters;
using Fluid.Values;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Fluid.Tests.Integration
{
    // https://github.com/Shopify/liquid/blob/master/test/integration/standard_filter_test.rb

    public class StandardFilterTests
    {
        private List<Statement> Parse(string source)
        {
            FluidTemplate.TryParse(source, out var template, out var errors);
            return template.Statements;
        }

        [Fact]
        public async Task TestSize()
        {
            Assert.Equal(3, (await ArrayFilters.Size(FluidValue.Create(new [] {1, 2, 3}), FilterArguments.Empty, new TemplateContext())).ToNumberValue());
            Assert.Equal(0, (await ArrayFilters.Size(FluidValue.Create(new int[0]), FilterArguments.Empty, new TemplateContext())).ToNumberValue());
            Assert.Equal(0, (await ArrayFilters.Size(NilValue.Instance, FilterArguments.Empty, new TemplateContext())).ToNumberValue());
        }

        [Theory]
        [InlineData("testing", "Testing")]
        [InlineData("", null)]
        public void TestDowncase(string expected, object input)
        {
            Assert.Equal(expected, StringFilters.Downcase(FluidValue.Create(input), FilterArguments.Empty, new TemplateContext()).ToObjectValue());
        }

        [Theory]
        [InlineData("TESTING", "Testing")]
        [InlineData("", null)]
        public void TestUpcase(string expected, object input)
        {
            Assert.Equal(expected, StringFilters.Upcase(FluidValue.Create(input), FilterArguments.Empty, new TemplateContext()).ToObjectValue());
        }

        [Theory]
        [InlineData("oob", "foobar", 1, 3)]
        [InlineData("oobar", "foobar", 1, 1000)]
        [InlineData("", "foobar", 1, 0)]
        [InlineData("o", "foobar", 1, 1)]
        [InlineData("bar", "foobar", 3, 3)]
        [InlineData("ar", "foobar", -2, 2)]
        [InlineData("ar", "foobar", -2, 1000)]
        [InlineData("r", "foobar", -1)]
        [InlineData("", null, 0)]
        [InlineData("", "foobar", 100, 10)]
        [InlineData("", "foobar", -100, 10)]
        [InlineData("oob", "foobar", "1", "3")]
        public void TestSlice(string expected, object input, params object[] arguments)
        {
            Assert.Equal(expected, StringFilters.Slice(FluidValue.Create(input), new FilterArguments(arguments), new TemplateContext()).ToObjectValue());
        }

        [Theory]
        [InlineData("foobar", null, null)]
        [InlineData("foobar", 0, "")]
        public void TestSliceArgument(object input, params object[] arguments)
        {
            Assert.Throws<ArgumentException>(() => StringFilters.Slice(FluidValue.Create(input), new FilterArguments(arguments), new TemplateContext()).ToObjectValue());
        }
        
        [Theory]
        [InlineData("oob", 1, 3)]
        [InlineData("oobar", 1, 1000)]
        [InlineData("", 1, 0)]
        [InlineData("o", 1, 1)]
        [InlineData("bar", 3, 3)]
        [InlineData("ar", -2, 2)]
        [InlineData("ar", -2, 1000)]
        [InlineData("r", -1)]
        [InlineData("", 100, 10)]
        [InlineData("", -100, 10)]
        public void TestSliceOnArrays(string expected, params object[] arguments)
        {
            var foobar = new object [] { 'f', 'o', 'o', 'b', 'a', 'r' };
            
            var result = StringFilters.Slice(FluidValue.Create(foobar), new FilterArguments(arguments), new TemplateContext());
            Assert.IsType<ArrayValue>(result);

            string resultString = "";
            foreach (var c in result.ToObjectValue() as object[])
            {
                resultString += c.ToString();
            }

            Assert.Equal(expected, resultString);
        }

        [Theory]
        [InlineData("1234...", "1234567890", 7)]
        [InlineData("1234567890", "1234567890", 20)]
        [InlineData("...", "1234567890", 0)]
        [InlineData("1234567890", "1234567890")]
        [InlineData("测试...", "测试测试测试测试", 5)]
        [InlineData("12341", "1234567890", 5, 1)]
        public void TestTruncate(string expected, object input, object length = null, object truncate = null)
        {
            Assert.Equal(expected, StringFilters.Truncate(FluidValue.Create(input), new FilterArguments(length, truncate), new TemplateContext()).ToObjectValue());
        }
    }    
}
