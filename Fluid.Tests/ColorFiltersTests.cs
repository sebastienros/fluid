using Fluid.Values;
using Fluid.Filters;
using Xunit;

namespace Fluid.Tests
{
    public class ColorFiltersTests
    {
        [Theory]
        [InlineData("#ffffff", "rgb(255, 255, 255)")]
        [InlineData("#fff", "rgb(255, 255, 255)")]
        [InlineData("#000", "rgb(0, 0, 0)")]
        [InlineData("#f00", "rgb(255, 0, 0)")]
        [InlineData("#0f0", "rgb(0, 255, 0)")]
        [InlineData("#00f", "rgb(0, 0, 255)")]
        [InlineData("#7ab55c", "rgb(122, 181, 92)")]
        public void ToRgb(string hexColor, string expected)
        {
            // Arrange
            var input = new StringValue(hexColor);
            var context = new TemplateContext();

            // Act
            var result = ColorFilters.ToRgb(input, FilterArguments.Empty, context);

            // Assert
            Assert.Equal(expected, result.ToStringValue());
        }

        [Theory]
        [InlineData("rgb(255, 255, 255)", "#ffffff")]
        [InlineData("rgb(0, 0, 0)", "#000000")]
        [InlineData("rgb(255, 0, 0)", "#ff0000")]
        [InlineData("rgb(0, 255, 0)", "#00ff00")]
        [InlineData("rgb(0, 0, 255)", "#0000ff")]
        [InlineData("rgb(122, 181, 92)", "#7ab55c")]
        public void ToHex(string rgbColor, string expected)
        {
            // Arrange
            var input = new StringValue(rgbColor);
            var context = new TemplateContext();

            // Act
            var result = ColorFilters.ToHex(input, FilterArguments.Empty, context);

            // Assert
            Assert.Equal(expected, result.ToStringValue());
        }
    }
}
