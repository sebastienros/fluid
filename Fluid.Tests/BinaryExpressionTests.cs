using System;
using System.Collections.Generic;
using Xunit;

namespace Fluid.Tests
{
    public class BinaryExpressionTests
    {
        private void Check(string source, string expected, Action<TemplateContext> init = null)
        {
            FluidTemplate.TryParse(source, out var template, out var messages);

            var context = new TemplateContext();
            init?.Invoke(context);

            var result = template.Render(context);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("1 == 1", "true")]
        [InlineData("1 == 2", "false")]
        public void EqualBinaryExpressionIsEvaluated(string source, string expected)
        {
            Check("{{" + source + "}}", expected);
        }

        [Theory]
        [InlineData("1 != 1", "false")]
        [InlineData("1 != 2", "true")]
        [InlineData("1 <> 1", "false")]
        [InlineData("1 <> 2", "true")]
        public void NotEqualBinaryExpressionIsEvaluated(string source, string expected)
        {
            Check("{{" + source + "}}", expected);
        }

        [Theory]
        [InlineData("1 + 1", "2")]
        [InlineData("'1' + '2'", "12")]
        public void AddBinaryExpressionIsEvaluated(string source, string expected)
        {
            Check("{{" + source + "}}", expected);
        }

        [Theory]
        [InlineData("1 - 1", "0")]
        public void SubstractBinaryExpressionIsEvaluated(string source, string expected)
        {
            Check("{{" + source + "}}", expected);
        }

        [Theory]
        [InlineData("6 / 3", "2")]
        [InlineData("6 / 0", "")]
        [InlineData("'a' / 'b'", "")]
        public void DivideBinaryExpressionIsEvaluated(string source, string expected)
        {
            Check("{{" + source + "}}", expected);
        }

        [Theory]
        [InlineData("6 * 3", "18")]
        public void MultiplyBinaryExpressionIsEvaluated(string source, string expected)
        {
            Check("{{" + source + "}}", expected);
        }

        [Theory]
        [InlineData("6 % 3", "0")]
        public void ModuloBinaryExpressionIsEvaluated(string source, string expected)
        {
            Check("{{" + source + "}}", expected);
        }

        [Theory]
        [InlineData("true and true", "true")]
        [InlineData("true and false", "false")]
        [InlineData("false and false", "false")]
        public void AndBinaryExpressionIsEvaluated(string source, string expected)
        {
            Check("{{" + source + "}}", expected);
        }

        [Theory]
        [InlineData("true or true", "true")]
        [InlineData("true or false", "true")]
        [InlineData("false or false", "false")]
        public void OrBinaryExpressionIsEvaluated(string source, string expected)
        {
            Check("{{" + source + "}}", expected);
        }

        [Theory]
        [InlineData("'abc' contains 'a'", "true")]
        [InlineData("'abc' contains 'd'", "false")]
        [InlineData("x contains 'a'", "true")]
        [InlineData("x contains 'x'", "false")]
        [InlineData("y contains 'a'", "true")]
        [InlineData("y contains 'x'", "false")]
        public void ContainsBinaryExpressionIsEvaluated(string source, string expected)
        {
            Check("{{" + source + "}}", expected, context =>
            {
                context.SetValue("x", new [] { "a", "b", "c" });
                context.SetValue("y", new List<string> { "a", "b", "c" });
            });
        }

        [Theory]
        [InlineData("10 < 10", "false")]
        [InlineData("10 <= 10", "true")]
        public void LowerThanBinaryExpressionIsEvaluated(string source, string expected)
        {
            Check("{{" + source + "}}", expected);
        }

        [Theory]
        [InlineData("10 > 10", "false")]
        [InlineData("10 >= 10", "true")]
        public void GreaterThanBinaryExpressionIsEvaluated(string source, string expected)
        {
            Check("{{" + source + "}}", expected);
        }

        [Theory]
        [InlineData("'' == empty", "true")]
        [InlineData("'a' == empty", "false")]
        [InlineData("x == empty", "true")]
        [InlineData("y == empty", "false")]
        public void EmptyValue(string source, string expected)
        {
            Check("{{" + source + "}}", expected, context =>
            {
                context.SetValue("x", new string[0]);
                context.SetValue("y", new List<string>{ "foo" });
            });
        }
    }
}
