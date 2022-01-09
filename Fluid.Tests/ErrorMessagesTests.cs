using Fluid.Parser;
using Xunit;

namespace Fluid.Tests
{
    public class ErrorMessagesTests
    {
#if COMPILED
        private static FluidParser _parser = new FluidParser().Compile();
#else
        private static FluidParser _parser = new FluidParser();
#endif

        [Theory]
        [InlineData("{% assign a 'b' %}", ErrorMessages.EqualAfterAssignIdentifier)]
        [InlineData("{% assign 'foo' %}", ErrorMessages.IdentifierAfterAssign)]
        [InlineData("{% assign %}", ErrorMessages.IdentifierAfterAssign)]
        [InlineData("{% assign a = | filter %}", ErrorMessages.LogicalExpressionStartsFilter)]
        [InlineData("{% assign a = %}", ErrorMessages.LogicalExpressionStartsFilter)]
        [InlineData("{% assign a = b | %}", ErrorMessages.IdentifierAfterPipe)]
        [InlineData("{% assign a = 1 | plus 2 %}", ErrorMessages.ExpectedTagEnd)]
        [InlineData("{{ 1 | plus 2 }}", ErrorMessages.ExpectedOutputEnd)]
        [InlineData("{% assing hello = 'world' %}", "Unknown tag 'assing' at (1:10)")]
        [InlineData("{% if true %} {{ foo }", ErrorMessages.ExpectedOutputEnd)]
        public void WrongTemplateShouldRenderErrorMessage(string source, string expected)
        {
            Assert.False(_parser.TryParse(source, out var _, out var error));
            Assert.StartsWith(expected, error); // e.g., message then 'at (1:15)'
        }

        [Theory]
        [InlineData("  {% assign a 'b' %}")]
        [InlineData("{% assign a = 'b' %}\n  {% assign a 'b' %}")]
        [InlineData("  {% assign a 'b' %}\n  {% assign a = 'b' %}")]
        [InlineData("  {% assign a 'b' %}\n\r  {% assign a = 'b' %}")]
        [InlineData("  {% assign a 'b' %}\n{% assign a = 'b' %}\n  {% assign a 'b' %}")]
        public void ErrorMessageContainsLineSource(string source)
        {
            Assert.False(_parser.TryParse(source, out var _, out var error));
            Assert.Contains("Source:\n  {% assign a 'b' %}", error);
        }
    }
}
