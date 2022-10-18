using Fluid.Filters;
using Fluid.Tests.Extensions;
using Fluid.Values;
using System.Globalization;
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
        [InlineData("#7bb65d", "rgb(123, 182, 93)")]
        [InlineData("hsl(0, 0%, 100%)", "rgb(255, 255, 255)")]
        [InlineData("hsl(0, 0%, 0%)", "rgb(0, 0, 0)")]
        [InlineData("hsl(0, 100%, 50%)", "rgb(255, 0, 0)")]
        [InlineData("hsl(100, 38%, 54%)", "rgb(123, 182, 93)")]
        [InlineData("hsl(120, 100%, 50%)", "rgb(0, 255, 0)")]
        [InlineData("hsl(240, 100%, 50%)", "rgb(0, 0, 255)")]
        [InlineData("hsl(300, 100%, 25%)", "rgb(128, 0, 128)")]
        [InlineData("hsla(0, 100%, 50%, 0.5)", "rgba(255, 0, 0, 0.5)")]
        public void ToRgb(string color, string expected)
        {
            // Arrange
            var input = new StringValue(color);
            var context = new TemplateContext();

            // Act
            var result = ColorFilters.ToRgb(input, FilterArguments.Empty, context);

            // Assert
            Assert.Equal(expected, result.Result.ToStringValue());
        }

        [Theory]
        [InlineData("en-US")]
        [InlineData("de")]
        [InlineData("fr-FR")]
        [InlineData("zh-Hans")]
        public void ToRgbShouldNotBeAffectedByCurrentCulture(string culture)
        {
            // Arrange
            SetCurrentCulture(culture);

            var input = new StringValue("hsla(0.5, 77.3%, 49.1%, 0.5)");
            var context = new TemplateContext();

            // Act
            var result = ColorFilters.ToRgb(input, FilterArguments.Empty, context);

            // Assert
            Assert.Equal("rgba(222, 30, 28, 0.5)", result.Result.ToStringValue());
        }

        [Theory]
        [InlineData("rgb(255, 255, 255)", "#ffffff")]
        [InlineData("rgb(0, 0, 0)", "#000000")]
        [InlineData("rgb(255, 0, 0)", "#ff0000")]
        [InlineData("rgb(0, 255, 0)", "#00ff00")]
        [InlineData("rgb(0, 0, 255)", "#0000ff")]
        [InlineData("rgb(123, 182, 93)", "#7bb65d")]
        [InlineData("rgba(123, 182, 93, 0.5)", "#7bb65d")]
        [InlineData("rgb(0,0,0)", "#000000")]
        [InlineData("rgb( 0,0,0 )", "#000000")]
        [InlineData("rgb( 0, 0    ,0 )", "#000000")]
        [InlineData("rgb(0,0,)", "")]
        [InlineData("hsl(0, 0%, 100%)", "#ffffff")]
        [InlineData("hsl(0, 0%, 0%)", "#000000")]
        [InlineData("hsl(0, 100%, 50%)", "#ff0000")]
        [InlineData("hsl(100, 38%, 54%)", "#7bb65d")]
        [InlineData("hsl(120, 100%, 50%)", "#00ff00")]
        [InlineData("hsl(240, 100%, 50%)", "#0000ff")]
        [InlineData("hsl(300, 100%, 25%)", "#800080")]
        [InlineData("hsl(300, 100%, 25%, 0.5)", "#800080")]
        public void ToHex(string color, string expected)
        {
            // Arrange
            var input = new StringValue(color);
            var context = new TemplateContext();

            // Act
            var result = ColorFilters.ToHex(input, FilterArguments.Empty, context);

            // Assert
            Assert.Equal(expected, result.Result.ToStringValue());
        }

        [Theory]
        [InlineData("en-US")]
        [InlineData("de")]
        [InlineData("fr-FR")]
        [InlineData("zh-Hans")]
        public void ToHexShouldNotBeAffectedByCurrentCulture(string culture)
        {
            // Arrange
            SetCurrentCulture(culture);

            var input = new StringValue("hsla(0.5, 77.3%, 49.1%, 0.5)");
            var context = new TemplateContext();

            // Act
            var result = ColorFilters.ToHex(input, FilterArguments.Empty, context);

            // Assert
            Assert.Equal("#de1e1c", result.Result.ToStringValue());
        }

        [Theory]
        [InlineData("#fff", "hsl(0, 0%, 100%)")]
        [InlineData("#000", "hsl(0, 0%, 0%)")]
        [InlineData("#f00", "hsl(0, 100%, 50%)")]
        [InlineData("#0f0", "hsl(120, 100%, 50%)")]
        [InlineData("#00f", "hsl(240, 100%, 50%)")]
        [InlineData("#800080", "hsl(300, 100%, 25%)")]
        [InlineData("rgb(255, 255, 255)", "hsl(0, 0%, 100%)")]
        [InlineData("rgb(0, 0, 0)", "hsl(0, 0%, 0%)")]
        [InlineData("rgb(255, 0, 0)", "hsl(0, 100%, 50%)")]
        [InlineData("rgb(0, 255, 0)", "hsl(120, 100%, 50%)")]
        [InlineData("rgb(0, 0, 255)", "hsl(240, 100%, 50%)")]
        [InlineData("rgb(128, 0, 128)", "hsl(300, 100%, 25%)")]
        [InlineData("rgba(255, 0, 0, 0.5)", "hsla(0, 100%, 50%, 0.5)")]
        public void ToHsl(string color, string expected)
        {
            // Arrange
            var input = new StringValue(color);
            var context = new TemplateContext();

            // Act
            var result = ColorFilters.ToHsl(input, FilterArguments.Empty, context);

            // Assert
            Assert.Equal(expected, result.Result.ToStringValue());
        }

        [Theory]
        [InlineData("en-US")]
        [InlineData("de")]
        [InlineData("fr-FR")]
        [InlineData("zh-Hans")]
        public void ToHslShouldNotBeAffectedByCurrentCulture(string culture)
        {
            // Arrange
            SetCurrentCulture(culture);

            var input = new StringValue("rgba(124, 26, 1, 0.5)");
            var context = new TemplateContext();

            // Act
            var result = ColorFilters.ToHsl(input, FilterArguments.Empty, context);

            // Assert
            Assert.Equal("hsla(12, 98%, 25%, 0.5)", result.Result.ToStringValue());
        }

        [Theory]
        [InlineData("#7bb65d", new object[] { "red" }, "123")]
        [InlineData("#7bb65d", new object[] { "green" }, "182")]
        [InlineData("#7bb65d", new object[] { "blue" }, "93")]
        [InlineData("#7bb65d", new object[] { "alpha" }, "1")]
        [InlineData("#7bb65d", new object[] { "hue" }, "100")]
        [InlineData("#7bb65d", new object[] { "saturation" }, "38")]
        [InlineData("#7bb65d", new object[] { "lightness" }, "54")]
        [InlineData("rgb(123, 182, 93)", new object[] { "red" }, "123")]
        [InlineData("rgb(123, 182, 93)", new object[] { "green" }, "182")]
        [InlineData("rgb(123, 182, 93)", new object[] { "blue" }, "93")]
        [InlineData("rgb(123, 182, 93)", new object[] { "alpha" }, "1")]
        [InlineData("rgba(123, 182, 93, 0.5)", new object[] { "alpha" }, "0.5")]
        [InlineData("rgb(123, 182, 93)", new object[] { "hue" }, "100")]
        [InlineData("rgb(123, 182, 93)", new object[] { "saturation" }, "38")]
        [InlineData("rgb(123, 182, 93)", new object[] { "lightness" }, "54")]
        [InlineData("hsl(100, 38%, 54%)", new object[] { "red" }, "123")]
        [InlineData("hsl(100, 38%, 54%)", new object[] { "green" }, "182")]
        [InlineData("hsl(100, 38%, 54%)", new object[] { "blue" }, "93")]
        [InlineData("hsl(100, 38%, 54%)", new object[] { "alpha" }, "1")]
        [InlineData("hsl(100, 38%, 54%, 0.5)", new object[] { "alpha" }, "0.5")]
        [InlineData("hsl(100, 38%, 54%)", new object[] { "hue" }, "100")]
        [InlineData("hsl(100, 38%, 54%)", new object[] { "saturation" }, "38")]
        [InlineData("hsl(100, 38%, 54%)", new object[] { "lightness" }, "54")]
        public void ColorExtract(string color, object[] arguments, string expected)
        {
            // Arrange
            var input = new StringValue(color);
            var context = new TemplateContext();

            // Act
            var result = ColorFilters.ColorExtract(input, arguments.ToFilterArguments(), context);

            // Assert
            Assert.Equal(expected, result.Result.ToStringValue());
        }

        [Theory]
        [InlineData("en-US")]
        [InlineData("de")]
        [InlineData("fr-FR")]
        [InlineData("zh-Hans")]
        public void ColorExtractShouldNotBeAffectedByCurrentCulture(string culture)
        {
            // Arrange
            SetCurrentCulture(culture);

            var input = new StringValue("hsl(100, 38%, 54%, 0.5)");
            var context = new TemplateContext();
            var arguments = new FluidValue[] {
                FluidValue.Create("alpha", TemplateOptions.Default),
            };

            // Act
            var result = ColorFilters.ColorExtract(input, new FilterArguments(arguments), context);

            // Assert
            Assert.Equal("0.5", result.Result.ToStringValue());
        }

        [Theory]
        [InlineData("#7bb65d", new object[] { "red", 255 }, "#ffb65d")]
        [InlineData("#7bb65d", new object[] { "green", 255 }, "#7bff5d")]
        [InlineData("#7bb65d", new object[] { "blue", 255 }, "#7bb6ff")]
        [InlineData("#7bb65d", new object[] { "alpha", 0.5 }, "rgba(123, 182, 93, 0.5)")]
        [InlineData("#7bb65d", new object[] { "hue", 50 }, "#b6a75d")]
        [InlineData("#7bb65d", new object[] { "saturation", 50 }, "#76c44f")]
        [InlineData("#7bb65d", new object[] { "lightness", 50 }, "#6fb04f")]
        [InlineData("rgb(123, 182, 93)", new object[] { "red", 255 }, "rgb(255, 182, 93)")]
        [InlineData("rgb(123, 182, 93)", new object[] { "green", 255 }, "rgb(123, 255, 93)")]
        [InlineData("rgb(123, 182, 93)", new object[] { "blue", 255 }, "rgb(123, 182, 255)")]
        [InlineData("rgba(123, 182, 93)", new object[] { "alpha", 0.5 }, "rgba(123, 182, 93, 0.5)")]
        [InlineData("rgb(123, 182, 93)", new object[] { "hue", 50 }, "rgb(182, 167, 93)")]
        [InlineData("rgb(123, 182, 93)", new object[] { "saturation", 50 }, "rgb(118, 196, 79)")]
        [InlineData("rgb(123, 182, 93)", new object[] { "lightness", 50 }, "rgb(111, 176, 79)")]
        [InlineData("hsl(100, 38%, 54%)", new object[] { "red", 255 }, "hsl(33, 100%, 68%)")]
        [InlineData("hsl(100, 38%, 54%)", new object[] { "green", 255 }, "hsl(109, 100%, 68%)")]
        [InlineData("hsl(100, 38%, 54%)", new object[] { "blue", 255 }, "hsl(213, 100%, 74%)")]
        [InlineData("hsl(100, 38%, 54%, 0.5)", new object[] { "alpha", 0.5 }, "hsla(100, 38%, 54%, 0.5)")]
        [InlineData("hsl(100, 38%, 54%)", new object[] { "hue", 50 }, "hsl(50, 38%, 54%)")]
        [InlineData("hsl(100, 38%, 54%)", new object[] { "saturation", 50 }, "hsl(100, 50%, 54%)")]
        [InlineData("hsl(100, 38%, 54%)", new object[] { "lightness", 50 }, "hsl(100, 38%, 50%)")]
        public void ColorModify(string color, object[] arguments, string expected)
        {
            // Arrange
            var input = new StringValue(color);
            var context = new TemplateContext();

            // Act
            var result = ColorFilters.ColorModify(input, arguments.ToFilterArguments(), context);

            // Assert
            Assert.Equal(expected, result.Result.ToStringValue());
        }

        [Theory]
        [InlineData("en-US")]
        [InlineData("de")]
        [InlineData("fr-FR")]
        [InlineData("zh-Hans")]
        public void ColorModifyShouldNotBeAffectedByCurrentCulture(string culture)
        {
            // Arrange
            SetCurrentCulture(culture);

            var input = new StringValue("hsla(100, 38%, 54%, 0.5)");
            var context = new TemplateContext();
            var arguments = new FluidValue[] {
                FluidValue.Create("alpha", TemplateOptions.Default),
                FluidValue.Create("0.8", TemplateOptions.Default),
            };

            // Act
            var result = ColorFilters.ColorModify(input, new FilterArguments(arguments), context);

            // Assert
            Assert.Equal("hsla(100, 38%, 54%, 0.8)", result.Result.ToStringValue());
        }

        [Theory]
        [InlineData("#7bb65d", 154.21)]
        [InlineData("rgb(123, 182, 93)", 154.21)]
        [InlineData("hsl(100, 38%, 54%)", 154.21)]
        public void CalculateBrightness(string color, decimal expected)
        {
            // Arrange
            var input = new StringValue(color);
            var context = new TemplateContext();

            // Act
            var result = ColorFilters.CalculateBrightness(input, FilterArguments.Empty, context);

            // Assert
            Assert.Equal(expected, result.Result.ToNumberValue());
        }

        [Theory]
        [InlineData("#7bb65d", new object[] { 30 }, "#6fd93a")]
        [InlineData("rgb(123, 182, 93)", new object[] { 30 }, "rgb(111, 217, 58)")]
        [InlineData("hsl(100, 38%, 54%)", new object[] { 30 }, "hsl(100, 68%, 54%)")]
        public void ColorSaturate(string color, object[] arguments, string expected)
        {
            // Arrange
            var input = new StringValue(color);
            var context = new TemplateContext();

            // Act
            var result = ColorFilters.ColorSaturate(input, arguments.ToFilterArguments(), context);

            // Assert
            Assert.Equal(expected, result.Result.ToStringValue());
        }

        [Theory]
        [InlineData("#7bb65d", new object[] { 30 }, "#879380")]
        [InlineData("rgb(123, 182, 93)", new object[] { 30 }, "rgb(135, 147, 128)")]
        [InlineData("hsl(100, 38%, 54%)", new object[] { 30 }, "hsl(100, 8%, 54%)")]
        public void ColorDesaturate(string color, object[] arguments, string expected)
        {
            // Arrange
            var input = new StringValue(color);
            var context = new TemplateContext();

            // Act
            var result = ColorFilters.ColorDesaturate(input, arguments.ToFilterArguments(), context);

            // Assert
            Assert.Equal(expected, result.Result.ToStringValue());
        }

        [Theory]
        [InlineData("#7bb65d", new object[] { 30 }, "#d1e6c7")]
        [InlineData("rgb(123, 182, 93)", new object[] { 30 }, "rgb(209, 230, 199)")]
        [InlineData("hsl(100, 38%, 54%)", new object[] { 30 }, "hsl(100, 38%, 84%)")]
        public void ColorLighten(string color, object[] arguments, string expected)
        {
            // Arrange
            var input = new StringValue(color);
            var context = new TemplateContext();

            // Act
            var result = ColorFilters.ColorLighten(input, arguments.ToFilterArguments(), context);

            // Assert
            Assert.Equal(expected, result.Result.ToStringValue());
        }

        [Theory]
        [InlineData("#7bb65d", new object[] { 30 }, "#355426")]
        [InlineData("rgb(123, 182, 93)", new object[] { 30 }, "rgb(53, 84, 38)")]
        [InlineData("hsl(100, 38%, 54%)", new object[] { 30 }, "hsl(100, 38%, 24%)")]
        public void ColorDarken(string color, object[] arguments, string expected)
        {
            // Arrange
            var input = new StringValue(color);
            var context = new TemplateContext();

            // Act
            var result = ColorFilters.ColorDarken(input, arguments.ToFilterArguments(), context);

            // Assert
            Assert.Equal(expected, result.Result.ToStringValue());
        }

        [Theory]
        [InlineData("#ff0000", new object[] { "#abcdef" }, 528)]
        [InlineData("rgb(255, 0, 0)", new object[] { "rgb(171, 205, 239)" }, 528)]
        [InlineData("hsl(0, 100%, 50%)", new object[] { "hsl(210, 68%, 80.4%)" }, 528)]
        public void ColorDifference(string color, object[] arguments, decimal expected)
        {
            // Arrange
            var input = new StringValue(color);
            var context = new TemplateContext();

            // Act
            var result = ColorFilters.GetColorDifference(input, arguments.ToFilterArguments(), context);

            // Assert
            Assert.Equal(expected, result.Result.ToNumberValue());
        }

        [Theory]
        [InlineData("#fff00f", new object[] { "#0b72ab" }, 129)]
        [InlineData("rgb(255, 240, 15)", new object[] { "rgb(11, 114, 171)" }, 129)]
        [InlineData("hsl(56, 100%, 53%)", new object[] { "hsl(201.4, 87.9%, 35.7%)" }, 129)]
        public void BrightnessDifference(string color, object[] arguments, decimal expected)
        {
            // Arrange
            var input = new StringValue(color);
            var context = new TemplateContext();

            // Act
            var result = ColorFilters.GetColorBrightnessDifference(input, arguments.ToFilterArguments(), context);

            // Assert
            Assert.Equal(expected, result.Result.ToNumberValue());
        }

        [Theory]
        [InlineData("#495859", new object[] { "#fffffb" }, 7.4)]
        [InlineData("rgb(73, 88, 89)", new object[] { "#fffffb" }, 7.4)]
        [InlineData("hsl(183.8, 9.9%, 31.8%)", new object[] { "#fffffb" }, 7.4)]
        public void ColorContrast(string color, object[] arguments, decimal expected)
        {
            // Arrange
            var input = new StringValue(color);
            var context = new TemplateContext();

            // Act
            var result = ColorFilters.GetColorContrast(input, arguments.ToFilterArguments(), context);

            // Assert
            Assert.Equal(expected, result.Result.ToNumberValue());
        }

        private static void SetCurrentCulture(string culture)
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        }
    }
}
