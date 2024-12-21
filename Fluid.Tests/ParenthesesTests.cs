using Fluid.Parser;
using Xunit;

namespace Fluid.Tests
{
    public class ParenthesesTests
    {
#if COMPILED
        private static FluidParser _parser = new FluidParser(new FluidParserOptions { AllowParentheses = true }).Compile();
#else
        private static FluidParser _parser = new FluidParser(new FluidParserOptions { AllowParentheses = true });
#endif

        [Fact]
        public void ShouldGroupFilters()
        {
            Assert.True(_parser.TryParse("{{ 1 | plus : (2 | times: 3) }}", out var template, out var errors));
            Assert.Equal("7", template.Render());
        }

        [Fact]
        public void ShouldNotParseParentheses()
        {
            var options = new FluidParserOptions { AllowParentheses = false };

#if COMPILED
        var parser = new FluidParser(options).Compile();
#else
            var parser = new FluidParser(options);
#endif

            Assert.False(parser.TryParse("{{ 1 | plus : (2 | times: 3) }}", out var template, out var errors));
            Assert.Contains(ErrorMessages.ParenthesesNotAllowed, errors);
        }
    }
}
