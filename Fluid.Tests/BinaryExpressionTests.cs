using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Xunit;

namespace Fluid.Tests
{
    public class BinaryExpressionTests
    {
        private async Task CheckAsync(string source, string expected, Action<TemplateContext> init = null)
        {
            FluidTemplate.TryParse(source, out var template, out var messages);

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
            return CheckAsync("{{" + source + "}}", expected);
        }

        [Theory]
        [InlineData("1 != 1", "false")]
        [InlineData("1 != 2", "true")]
        [InlineData("1 <> 1", "false")]
        [InlineData("1 <> 2", "true")]
        public Task NotEqualBinaryExpressionIsEvaluated(string source, string expected)
        {
            return CheckAsync("{{" + source + "}}", expected);
        }

        [Theory]
        [InlineData("1 + 1", "2")]
        [InlineData("'1' + '2'", "12")]
        public Task AddBinaryExpressionIsEvaluated(string source, string expected)
        {
            return CheckAsync("{{" + source + "}}", expected);
        }

        [Theory]
        [InlineData("1 - 1", "0")]
        public Task SubstractBinaryExpressionIsEvaluated(string source, string expected)
        {
            return CheckAsync("{{" + source + "}}", expected);
        }

        [Theory]
        [InlineData("6 / 3", "2")]
        [InlineData("6 / 0", "")]
        [InlineData("'a' / 'b'", "")]
        public Task DivideBinaryExpressionIsEvaluated(string source, string expected)
        {
            return CheckAsync("{{" + source + "}}", expected);
        }

        [Theory]
        [InlineData("6 * 3", "18")]
        public Task MultiplyBinaryExpressionIsEvaluated(string source, string expected)
        {
            return CheckAsync("{{" + source + "}}", expected);
        }

        [Theory]
        [InlineData("6 % 3", "0")]
        public Task ModuloBinaryExpressionIsEvaluated(string source, string expected)
        {
            return CheckAsync("{{" + source + "}}", expected);
        }

        [Theory]
        [InlineData("true and true", "true")]
        [InlineData("true and false", "false")]
        [InlineData("false and false", "false")]
        public Task AndBinaryExpressionIsEvaluated(string source, string expected)
        {
            return CheckAsync("{{" + source + "}}", expected);
        }

        [Theory]
        [InlineData("true or true", "true")]
        [InlineData("true or false", "true")]
        [InlineData("false or false", "false")]
        public Task OrBinaryExpressionIsEvaluated(string source, string expected)
        {
            return CheckAsync("{{" + source + "}}", expected);
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
            return CheckAsync("{{" + source + "}}", expected, context =>
            {
                context.SetValue("x", new [] { "a", "b", "c" });
                context.SetValue("y", new List<string> { "a", "b", "c" });
            });
        }

        [Theory]
        [InlineData("'Strasse' contains 'ß'", "false")]
        [InlineData("'ss' == 'ß'", "false")]
        public Task StringComparisonsShouldBeOrdinal(string source, string expected)
        {
            return CheckAsync("{{" + source + "}}", expected, context =>
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
            return CheckAsync("{{" + source + "}}", expected);
        }

        [Theory]
        [InlineData("10 > 10", "false")]
        [InlineData("10 >= 10", "true")]
        public Task GreaterThanBinaryExpressionIsEvaluated(string source, string expected)
        {
            return CheckAsync("{{" + source + "}}", expected);
        }

        [Theory]
        [InlineData("'' == empty", "true")]
        [InlineData("'a' == empty", "false")]
        [InlineData("x == empty", "true")]
        [InlineData("y == empty", "false")]
        public Task EmptyValue(string source, string expected)
        {
            return CheckAsync("{{" + source + "}}", expected, context =>
            {
                context.SetValue("x", new string[0]);
                context.SetValue("y", new List<string>{ "foo" });
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
            return CheckAsync("{% if " + source + " %}true{% else %}false{% endif %}", expected);
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
            return CheckAsync("{% if " + source + " %}true{% else %}false{% endif %}", expected, t => t.SetValue("var", value));
        }

    }
}
