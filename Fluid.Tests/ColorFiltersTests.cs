﻿using Fluid.Values;
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
            Assert.Equal(expected, result.ToStringValue());
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
            Assert.Equal(expected, result.ToStringValue());
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
            Assert.Equal(expected, result.ToStringValue());
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
            var result = ColorFilters.ColorExtract(input, new FilterArguments(arguments), context);

            // Assert
            Assert.Equal(expected, result.ToStringValue());
        }
    }
}
