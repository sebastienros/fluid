using Fluid.Filters;
using Fluid.Tests.Extensions;
using Fluid.Values;
using System.Globalization;
using System.Threading.Tasks;
using Xunit;

namespace Fluid.Tests;

public class ColorFiltersTests
{
#if COMPILED
    private static readonly FluidParser _parser = new FluidParser().Compile();
#else
    private static readonly FluidParser _parser = new FluidParser();
#endif

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
    [InlineData("#ABC", "rgb(170, 187, 204)")]
    [InlineData("#AABBCC", "rgb(170, 187, 204)")]
    [InlineData("hsl(120deg, 100%, 50%)", "rgb(0, 255, 0)")]
    [InlineData("hsl(100grad, 100%, 50%)", "rgb(128, 255, 0)")]
    [InlineData("hsl(1.5707963267948966rad, 100%, 50%)", "rgb(128, 255, 0)")]
    [InlineData("hsl(1rad, 50%, 50%)", "rgb(191, 186, 64)")]
    [InlineData("hsl(4, 50%, 50%, 50%)", "rgba(191, 72, 64, 0.5)")]
    [InlineData("hsl(.25turn, 100%, 50%)", "rgb(128, 255, 0)")]
    [InlineData("hsl(-.75turn, 100%, 50%)", "rgb(128, 255, 0)")]
    [InlineData("hsl(2.25turn, 100%, 50%)", "rgb(128, 255, 0)")]
    [InlineData("hsl(-270deg, 100%, 50%)", "rgb(128, 255, 0)")]
    [InlineData("hsl(810, 100%, 50%)", "rgb(128, 255, 0)")]
    [InlineData("hsla(0DEG, 100%, 50%, 50%)", "rgba(255, 0, 0, 0.5)")]
    [InlineData("hsl(0, 150%, 50%, -10%)", "rgba(255, 0, 0, 0)")]
    [InlineData("hsla(0, -10%, 150%, 200%)", "rgb(255, 255, 255)")]
    [InlineData("hsl(0, 100%, -10%, .4)", "rgba(0, 0, 0, 0.4)")]
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
    [InlineData("rgb(10.1, 11.2, 11.3, .4)", "#0a0b0b")]
    [InlineData("rgb(10%, 10%, 20%, 40%)", "#1a1a33")]
    [InlineData("rgba(100%, 50%, 0%, .5)", "#ff8000")]
    [InlineData("rgba(+1e2, 2E2, .5, 5e-1)", "#64c801")]
    [InlineData("rgb(10.5, 11.5, 12.5)", "#0b0c0d")]
    [InlineData("rgb(10.49, 11.49, 12.49)", "#0a0b0c")]
    [InlineData("rgb(-1, 256, 1e300)", "#00ffff")]
    [InlineData("rgb(-10%, 150%, 50%)", "#00ff80")]
    [InlineData("rgb(30%, 70%, 90%)", "#4db3e6")]
    [InlineData("rgb(\t255,\r\n0,\f0)", "#ff0000")]
    [InlineData("hsl(.5turn, 100%, 50%)", "#00ffff")]
    [InlineData("hsl(2e2grad, 100%, 50%, 50%)", "#00ffff")]
    [InlineData("hsl(360, 100%, 50%)", "#ff0000")]
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
    [InlineData("rgba(100%, 0%, 0%, 50%)", "hsla(0, 100%, 50%, 0.5)")]
    [InlineData("rgb(255.4, .1, .2, 40%)", "hsla(0, 100%, 50%, 0.4)")]
    [InlineData("rgba(255, 0, 0, -.5)", "hsla(0, 100%, 50%, 0)")]
    [InlineData("rgba(255, 0, 0, 150%)", "hsl(0, 100%, 50%)")]
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
    [InlineData("rgb(10.1, 11.2, 11.3, .4)", new object[] { "red" }, "10")]
    [InlineData("rgb(10%, 10%, 20%, 40%)", new object[] { "alpha" }, "0.4")]
    [InlineData("rgba(123, 182, 93, .123456789)", new object[] { "alpha" }, "0.123456789")]
    [InlineData("hsla(.25turn, 50%, 50%, 12.3456789%)", new object[] { "alpha" }, "0.123456789")]
    [InlineData("hsl(-100grad, 50%, 50%)", new object[] { "hue" }, "270")]
    [InlineData("hsl(450deg, 50%, 50%)", new object[] { "hue" }, "90")]
    [InlineData("hsl(100deg, 50%, 50%)", new object[] { "hue" }, "100")]
    [InlineData("hsl(1e300turn, 50%, 50%)", new object[] { "hue" }, "0")]
    [InlineData("hsl(-5e-324deg, 50%, 50%)", new object[] { "hue" }, "0")]
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

    [Theory]
    [InlineData("")]
    [InlineData("rgb(1, 2)")]
    [InlineData("rgb(1, 2, 3, .5, .6)")]
    [InlineData("rgb(1., 2, 3)")]
    [InlineData("rgb(1e, 2, 3)")]
    [InlineData("rgb(1e999, 2, 3)")]
    [InlineData("rgb(NaN, 2, 3)")]
    [InlineData("rgb(Infinity, 2, 3)")]
    [InlineData("rgb(1%%, 2%, 3%)")]
    [InlineData("rgb(1%, 2, 3%)")]
    [InlineData("rgba(1, 2, 3, NaN)")]
    [InlineData("rgba(1, 2, 3, Infinity)")]
    [InlineData("rgba(1, 2, 3, 1e999)")]
    [InlineData("rgba(1, 2, 3, 50%%)")]
    [InlineData("rgb(1 2 3 / .5)")]
    [InlineData("hsl(1rad, 50, 50%)")]
    [InlineData("hsl(1rad, 50%, 50)")]
    [InlineData("hsl(1rad, 50%%, 50%)")]
    [InlineData("hsl(1rad, 50%, 50%, 50%%)")]
    [InlineData("hsl(1foo, 50%, 50%)")]
    [InlineData("hsl(1%, 50%, 50%)")]
    [InlineData("hsl(1.rad, 50%, 50%)")]
    [InlineData("hsl(1e999turn, 50%, 50%)")]
    [InlineData("hsl(NaN, 50%, 50%)")]
    [InlineData("hsl(Infinity, 50%, 50%)")]
    [InlineData("hsl(0, 1e999%, 50%)")]
    [InlineData("hsl(0, 50%, 50%, NaN)")]
    [InlineData("hsl(0, 50%, 50%, 1e999)")]
    [InlineData("#ggg")]
    [InlineData("#aabbcg")]
    [InlineData("#abcd")]
    [InlineData("#aabbccdd")]
    public void InvalidColorShouldReturnEmpty(string color)
    {
        var result = ColorFilters.CalculateBrightness(new StringValue(color), FilterArguments.Empty, new TemplateContext());

        Assert.Same(EmptyValue.Instance, result.Result);
    }

    [Theory]
    [InlineData("color_to_hex")]
    [InlineData("color_extract: 'red'")]
    [InlineData("color_extract: 'green'")]
    [InlineData("color_extract: 'blue'")]
    [InlineData("color_extract: 'alpha'")]
    [InlineData("color_extract: 'hue'")]
    [InlineData("color_extract: 'saturation'")]
    [InlineData("color_extract: 'lightness'")]
    [InlineData("color_modify: 'alpha', .8")]
    [InlineData("color_modify: 'red', 200")]
    [InlineData("color_modify: 'green', 200")]
    [InlineData("color_modify: 'blue', 200")]
    [InlineData("color_modify: 'hue', 50")]
    [InlineData("color_modify: 'saturation', 50")]
    [InlineData("color_modify: 'lightness', 50")]
    [InlineData("color_saturate: 30")]
    [InlineData("color_desaturate: 30")]
    [InlineData("color_lighten: 30")]
    [InlineData("color_darken: 30")]
    [InlineData("color_brightness")]
    [InlineData("color_difference: other")]
    [InlineData("brightness_difference: other")]
    [InlineData("color_contrast: other")]
    public async Task NumericTypesShouldWorkAcrossColorFilters(string filter)
    {
        var options = new TemplateOptionsBuilder().WithColorFilters().Build();
        Assert.True(_parser.TryParse("{{ color | " + filter + " }}", out var template, out var error), error);

        foreach (var (original, extended) in new[]
        {
            ("rgba(123, 182, 93, 0.5)", "rgba(1.23e2, 182.1, 92.8, 50%)"),
            ("hsla(100, 38%, 54%, 0.5)", "hsla(100deg, 38%, 54%, 50%)"),
            ("rgba(128, 191, 64, 0.5)", "rgba(50%, 75%, 25%, 50%)")
        })
        {
            var context = new TemplateContext(options);
            context.SetValue("color", original);
            context.SetValue("other", "rgb(171, 205, 239)");
            var expected = await template.RenderAsync(context);

            context.SetValue("color", extended);
            context.SetValue("other", "rgb(171.1, 205.1, 239.1)");
            var result = await template.RenderAsync(context);

            Assert.NotEmpty(result);
            Assert.Equal(expected, result);
        }
    }

    [Theory]
    [InlineData("color_difference")]
    [InlineData("brightness_difference")]
    [InlineData("color_contrast")]
    public async Task SecondColorShouldSupportAnglesAndPercentages(string filter)
    {
        var options = new TemplateOptionsBuilder().WithColorFilters().Build();
        var context = new TemplateContext(options);
        context.SetValue("color", "#495859");
        Assert.True(_parser.TryParse("{{ color | " + filter + ": other }}", out var template, out var error), error);

        context.SetValue("other", "hsla(210, 68%, 80.4%, .5)");
        var expected = await template.RenderAsync(context);
        context.SetValue("other", "hsla(210deg, 68%, 80.4%, 50%)");

        var result = await template.RenderAsync(context);
        Assert.NotEmpty(result);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("de")]
    [InlineData("fr-FR")]
    [InlineData("zh-Hans")]
    public async Task NumericTypesShouldBeCultureIndependent(string culture)
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            SetCurrentCulture(culture);
            var options = new TemplateOptionsBuilder().WithColorFilters().Build();
            var context = new TemplateContext(options);
            Assert.True(_parser.TryParse(
                "{{ 'rgba(100%, 0%, 0%, 50%)' | color_to_hsl }}|" +
                "{{ 'hsla(.25turn, 100%, 50%, 50%)' | color_to_rgb }}|" +
                "{{ 'rgb(10.1, 11.2, 11.3, .4)' | color_to_hex }}|" +
                "{{ 'hsl(200grad, 100%, 50%)' | color_to_rgb | color_to_hex }}",
                out var template, out var error), error);

            Assert.Equal("hsla(0, 100%, 50%, 0.5)|rgba(128, 255, 0, 0.5)|#0a0b0b|#00ffff",
                await template.RenderAsync(context));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    private static void SetCurrentCulture(string culture)
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
    }
}
