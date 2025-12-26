using Fluid.Values;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Xunit;

namespace Fluid.Tests
{
    public class BinaryExpressionTests
    {
#if COMPILED
        private static FluidParser _parser = new FluidParser().Compile();
#else
        private static FluidParser _parser = new FluidParser();
#endif

        private async Task CheckAsync(string source, string expected, Action<TemplateContext> init = null)
        {
            _parser.TryParse("{% if " + source + " %}true{% else %}false{% endif %}", out var template, out var messages);

            var context = new TemplateContext();
            init?.Invoke(context);

            var result = await template.RenderAsync(context);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("1 == 1", "true")]
        [InlineData("1 == 2", "false")]
        public Task EqualBinaryExpressionIsEvaluated(string source, string expected)
        {
            return CheckAsync(source, expected);
        }

        [Theory]
        [InlineData("1 != 1", "false")]
        [InlineData("1 != 2", "true")]
        [InlineData("1 <> 1", "false")]
        [InlineData("1 <> 2", "true")]
        public Task NotEqualBinaryExpressionIsEvaluated(string source, string expected)
        {
            return CheckAsync(source, expected);
        }

        [Theory]
        [InlineData("true and true", "true")]
        [InlineData("true and false", "false")]
        [InlineData("false and false", "false")]
        public Task AndBinaryExpressionIsEvaluated(string source, string expected)
        {
            return CheckAsync(source, expected);
        }

        [Theory]
        [InlineData("true or true", "true")]
        [InlineData("true or false", "true")]
        [InlineData("false or false", "false")]
        public Task OrBinaryExpressionIsEvaluated(string source, string expected)
        {
            return CheckAsync(source, expected);
        }

        [Theory]
        [InlineData("'abc' contains 'a'", "true")]
        [InlineData("'abc' contains 'd'", "false")]
        [InlineData("x contains 'a'", "true")]
        [InlineData("x contains 'x'", "false")]
        [InlineData("y contains 'a'", "true")]
        [InlineData("y contains 'x'", "false")]
        public Task ContainsBinaryExpressionIsEvaluated(string source, string expected)
        {
            return CheckAsync(source, expected, context =>
            {
                context.SetValue("x", new[] { "a", "b", "c" });
                context.SetValue("y", new List<string> { "a", "b", "c" });
            });
        }

        [Theory]
        [InlineData("'Strasse' contains 'ß'", "false")]
        [InlineData("'ss' == 'ß'", "false")]
        public Task StringComparisonsShouldBeOrdinal(string source, string expected)
        {
            return CheckAsync(source, expected, context =>
            {
                // This is the default, but forcing it for clarity of the test
                // With invariant culture, Strasse and Straße are equal, and Liquid 
                // should not assume they are 
                context.CultureInfo = CultureInfo.InvariantCulture;
            });
        }

        [Theory]
        [InlineData("10 < 10", "false")]
        [InlineData("10 <= 10", "true")]
        public Task LowerThanBinaryExpressionIsEvaluated(string source, string expected)
        {
            return CheckAsync(source, expected);
        }

        [Theory]
        [InlineData("10 > 10", "false")]
        [InlineData("10 >= 10", "true")]
        public Task GreaterThanBinaryExpressionIsEvaluated(string source, string expected)
        {
            return CheckAsync(source, expected);
        }

        [Theory]
        [InlineData("'abc' startswith 'bc'", "false")]
        [InlineData("'abc' startswith 'ab'", "true")]
        [InlineData("x startswith 'b'", "false")]
        [InlineData("x startswith 'a'", "true")]
        [InlineData("y startswith 2", "false")]
        [InlineData("y startswith 1", "true")]
        [InlineData("z startswith 'a'", "false")]
        public Task StartsWithBinaryExpressionIsEvaluated(string source, string expected)
        {
            return CheckAsync(source, expected, context =>
            {
                context.SetValue("x", new[] { "a", "b", "c" });
                context.SetValue("y", new[] { 1, 2, 3 });
                context.SetValue("z", new string[0]);
            });
        }

        [Theory]
        [InlineData("'abc' endswith 'ab'", "false")]
        [InlineData("'abc' endswith 'bc'", "true")]
        [InlineData("x endswith 'b'", "false")]
        [InlineData("x endswith 'c'", "true")]
        [InlineData("y endswith 2", "false")]
        [InlineData("y endswith 3", "true")]
        [InlineData("z endswith 'a'", "false")]
        public Task EndsWithBinaryExpressionIsEvaluated(string source, string expected)
        {
            return CheckAsync(source, expected, context =>
            {
                context.SetValue("x", new[] { "a", "b", "c" });
                context.SetValue("y", new[] { 1, 2, 3 });
                context.SetValue("z", new string[0]);
            });
        }

        [Theory]
        [InlineData("'' == empty", "true")]
        [InlineData("'a' == empty", "false")]
        [InlineData("x == empty", "true")]
        [InlineData("y == empty", "false")]
        public Task EmptyValue(string source, string expected)
        {
            return CheckAsync(source, expected, context =>
            {
                context.SetValue("x", new string[0]);
                context.SetValue("y", new List<string> { "foo" });
            });
        }

        [Theory]
        [InlineData("''", "true")]
        [InlineData("'a'", "true")]
        [InlineData("blank", "true")]
        [InlineData("empty", "true")]
        [InlineData("0", "true")]
        [InlineData("1", "true")]
        [InlineData("abc", "false")]
        [InlineData("false", "false")]
        public Task TruthyFalsy(string source, string expected)
        {
            return CheckAsync(source, expected);
        }

        [Theory]
        [InlineData("true == true", "true", null)]
        [InlineData("true != true", "false", null)]
        [InlineData("0 > 0", "false", null)]
        [InlineData("1 > 0", "true", null)]
        [InlineData("0 < 1", "true", null)]
        [InlineData("0 <= 0", "true", null)]
        [InlineData("null <= 0", "false", null)]
        [InlineData("0 <= null", "false", null)]
        [InlineData("0 >= 0", "true", null)]
        [InlineData("'test' == 'test'", "true", null)]
        [InlineData("'test' != 'test'", "false", null)]
        [InlineData("var == 'hello there!'", "true", "hello there!")]
        [InlineData("'hello there!' == var", "true", "hello there!")]
        [InlineData("'hello there!' == true", "false", "hello there!")]
        [InlineData("'hello there!' == false", "false", "hello there!")]
        [InlineData("null <= null", "true", null)]
        [InlineData("null >= null", "true", null)]
        [InlineData("null < null", "false", null)]
        [InlineData("null > null", "false", null)]
        [InlineData("null == null", "true", null)]
        [InlineData("null != null", "false", null)]
        public Task StringLiteralTrue(string source, string expected, object value)
        {
            // https://github.com/Shopify/liquid/blob/master/test/integration/tags/statements_test.rb
            return CheckAsync(source, expected, t => t.SetValue("var", value));
        }

        [Theory]
        [InlineData("true or false and false", "true")]
        [InlineData("true and false and false or true", "false")]
        [InlineData("true and true and false == false", "true")]
        [InlineData("true and true and true == false", "false")]
        [InlineData("false or false or true == true", "true")]
        public Task OperatorsShouldBeEvaluatedFromRightToLeft(string source, string expected)
        {
            // https://shopify.github.io/liquid/basics/operators/
            return CheckAsync(source, expected);
        }

        [Theory]
        [InlineData("1 == 1 or 1 == 2 and 1 == 2", "true")]
        [InlineData("1 == 1 and 1 == 2 and 1 == 2 or 1 == 1", "false")]
        public Task OperatorsHavePriority(string source, string expected)
        {
            return CheckAsync(source, expected);
        }

        [Fact]
        public void DictionaryValuesShouldBeEqual()
        {
            var actual = new DictionaryValue(new FluidValueDictionaryFluidIndexable(new Dictionary<string, FluidValue>
            {
                { "stringProperty", StringValue.Create("testValue") }
            }));

            var expected = new DictionaryValue(new FluidValueDictionaryFluidIndexable(new Dictionary<string, FluidValue>
            {
                { "stringProperty", StringValue.Create("testValue") }
            }));

            Assert.True(actual.Equals(expected));
        }

        [Fact]
        public void ArrayValuesShouldBeEqual()
        {
            var actual = new ArrayValue(new FluidValue[]
            {
                StringValue.Create("testValue")
            });

            var expected = new ArrayValue(new FluidValue[]
            {
                StringValue.Create("testValue")
            });


            Assert.True(actual.Equals(expected));
        }

        [Theory]
        [InlineData("{{ 2 == 3 }}", "2")]
        [InlineData("{{ 5 == 5 }}", "5")]
        [InlineData("{{ 10 != 5 }}", "10")]
        [InlineData("{{ 10 > 5 }}", "10")]
        [InlineData("{{ 3 < 5 }}", "3")]
        [InlineData("{{ 10 >= 5 }}", "10")]
        [InlineData("{{ 3 <= 5 }}", "3")]
        [InlineData("{{ true and false }}", "true")]
        [InlineData("{{ true or false }}", "true")]
        [InlineData("{{ 'abc' contains 'a' }}", "abc")]
        [InlineData("{{ 'abc' startswith 'a' }}", "abc")]
        [InlineData("{{ 'abc' endswith 'c' }}", "abc")]
        public async Task BinaryExpressionsReturnLeftOperand(string source, string expected)
        {
            // Binary expressions should return the left operand according to Liquid standard
            _parser.TryParse(source, out var template, out var messages);
            var result = await template.RenderAsync();
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("{{ 2 == 3 | plus: 10 | minus: 3 }}", "9")]
        [InlineData("{{ 5 == 5 | plus: 3 }}", "8")]
        [InlineData("{{ 10 > 5 | minus: 2 }}", "8")]
        public async Task BinaryExpressionsWithFilters(string source, string expected)
        {
            // Binary expressions return left operand which can then be filtered
            _parser.TryParse(source, out var template, out var messages);
            var result = await template.RenderAsync();
            Assert.Equal(expected, result);
        }

        [Fact]
        public async Task ObjectValuesShouldCompareByValueNotReference()
        {
            // Create a simple class that overrides Equals
            var obj1 = new TestObject { Value = "test" };
            var obj2 = new TestObject { Value = "test" };

            _parser.TryParse("{% if obj1 == obj2 %}equal{% else %}not equal{% endif %}", out var template, out var messages);

            var context = new TemplateContext();
            context.SetValue("obj1", obj1);
            context.SetValue("obj2", obj2);

            var result = await template.RenderAsync(context);

            // obj1.Equals(obj2) returns true, so the comparison should return "equal"
            Assert.Equal("equal", result);
        }

        [Fact]
        public async Task ArrayContainsShouldCompareByValueNotReference()
        {
            var obj1 = new TestObject { Value = "test" };
            var obj2 = new TestObject { Value = "test" };
            var array = new[] { obj1 };

            _parser.TryParse("{% if array contains obj2 %}found{% else %}not found{% endif %}", out var template, out var messages);

            var context = new TemplateContext();
            context.SetValue("array", array);
            context.SetValue("obj2", obj2);

            var result = await template.RenderAsync(context);

            // obj1.Equals(obj2) returns true, so contains should return "found"
            Assert.Equal("found", result);
        }

        private class TestObject
        {
            public string Value { get; set; }

            public override bool Equals(object obj)
            {
                return obj is TestObject other && Value == other.Value;
            }

            public override int GetHashCode()
            {
                return Value?.GetHashCode() ?? 0;
            }
        }

        [Theory]
        [InlineData("'1' <= '1'", "true")]
        [InlineData("'1' <= '2'", "true")]
        [InlineData("'2' <= '1'", "false")]
        [InlineData("'a' <= 'b'", "true")]
        [InlineData("'b' <= 'a'", "false")]
        [InlineData("'ab' <= 'a'", "false")]
        [InlineData("'abc' <= 'ab'", "false")]
        [InlineData("'abc' <= 'abd'", "true")]
        [InlineData("'ab' <= 'abd'", "true")]
        public Task CompareString(string source, string expected)
        {
            return CheckAsync(source, expected);
        }

        [Theory]
        [InlineData("'1' == '1'", "true")]
        [InlineData("'1' == '01'", "false")]        
        [InlineData("'1' != '1'", "false")]
        [InlineData("'1' != '01'", "true")]
        [InlineData("'1' == 1", "false")]        
        [InlineData("'1' != 1", "true")]
        [InlineData("'1' == true", "false")]
        [InlineData("'1' != false", "true")]
        [InlineData("'1' == false", "false")]
        [InlineData("'0' == true", "false")]
        [InlineData("'0' == false", "false")]
        public Task StringEquality(string source, string expected)
        {
            return CheckAsync(source, expected);
        }

        [Theory]
        [InlineData("1 == 1", "true")]
        [InlineData("1 != 1", "false")]
        [InlineData("1 == 01", "true")]
        [InlineData("1 == '1'", "false")]
        [InlineData("1 != '1'", "true")]
        [InlineData("1 == true", "false")]
        [InlineData("1 != true", "true")]
        [InlineData("1 == false", "false")]
        [InlineData("0 == true", "false")]
        [InlineData("0 == false", "false")]
        public Task NumberEquality(string source, string expected)
        {
            return CheckAsync(source, expected);
        }        

        [Theory]
        [InlineData("true == true", "true")]
        [InlineData("false == false", "true")]
        [InlineData("true == false", "false")]
        [InlineData("true != true", "false")]
        [InlineData("false != false", "false")]
        [InlineData("true != false", "true")]
        [InlineData("true == '1'", "false")]
        [InlineData("true == 1", "false")]        
        [InlineData("false == '1'", "false")]
        [InlineData("false == 1", "false")]
        public Task BooleanEquality(string source, string expected)
        {
            return CheckAsync(source, expected);
        }   

        [Fact]
        public async Task ContainsShouldSupportAsyncWithContext()
        {
            // Create a custom FluidValue that uses ContainsAsync with TemplateContext
            var customValue = new CustomAsyncContainsValue(new[] { "a", "b", "c" });

            _parser.TryParse("{% if custom contains 'b' %}found{% else %}not found{% endif %}", out var template, out var messages);

            var context = new TemplateContext();
            context.SetValue("custom", customValue);

            var result = await template.RenderAsync(context);

            // The custom value should use ContainsAsync and find 'b'
            Assert.Equal("found", result);
        }

        private class CustomAsyncContainsValue : FluidValue
        {
            private readonly string[] _values;

            public CustomAsyncContainsValue(string[] values)
            {
                _values = values;
            }

            public override FluidValues Type => FluidValues.Array;

            public override bool Equals(FluidValue other) => false;

            public override bool ToBooleanValue() => true;

            public override decimal ToNumberValue() => _values.Length;

            public override string ToStringValue() => string.Join(",", _values);

            public override object ToObjectValue() => _values;

            public override async ValueTask<bool> ContainsAsync(FluidValue value, TemplateContext context)
            {
                // Simulate async operation
                await Task.Delay(1);
                var searchValue = value.ToStringValue(context);
                return Array.Exists(_values, v => v == searchValue);
            }
        }

    }
}
