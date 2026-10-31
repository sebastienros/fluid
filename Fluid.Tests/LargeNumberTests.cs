using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Fluid.Tests;

/// <summary>
/// Numbers too large for the type they are converted to, which used to throw OverflowException.
/// </summary>
public class LargeNumberTests
{
#if COMPILED
    private static readonly FluidParser _parser = new FluidParser().Compile();
#else
    private static readonly FluidParser _parser = new FluidParser();
#endif

    public sealed class Measurements
    {
        public double Ratio { get; set; }

        public float Single { get; set; }

        public double Field;
    }

    private static Task<string> RenderAsync(string source, Action<TemplateContext> setup = null)
    {
        var context = new TemplateContext().SetValue("items", new[] { 1, 2, 3 });
        setup?.Invoke(context);

        return _parser.Parse(source).RenderAsync(context).AsTask();
    }

    [Theory]
    [InlineData("[{{ items[9999999999] }}]", "[]")]
    [InlineData("[{{ items[-9999999999] }}]", "[]")]
    [InlineData("[{{ items[2147483648] }}]", "[]")]
    [InlineData("[{{ items[2] }}][{{ items[-1] }}]", "[3][3]")]
    public async Task IndexOutOfRangeIsNil(string source, string expected)
    {
        Assert.Equal(expected, await RenderAsync(source));
    }

    [Fact]
    public async Task IndexOutOfRangeIsNilOnEnumerables()
    {
        var result = await RenderAsync("[{{ seq[9999999999] }}][{{ seq[1] }}]", c => c.SetValue("seq", new[] { 1, 2, 3 }.Where(_ => true)));

        Assert.Equal("[][2]", result);
    }

    [Theory]
    [InlineData("[{{ 'abc' | slice: 99999999999 }}]", "[]")]
    [InlineData("[{{ 'abc' | slice: -99999999999 }}]", "[]")]
    [InlineData("[{{ 'abc' | slice: 1, 99999999999 }}]", "[bc]")]
    [InlineData("[{{ 'abc' | truncate: 99999999999 }}]", "[abc]")]
    [InlineData("[{{ 'a b c' | truncatewords: 99999999999 }}]", "[a b c]")]
    [InlineData("{% for i in items offset: 99999999999 %}{{ i }}{% else %}none{% endfor %}", "none")]
    [InlineData("{% for i in items limit: 99999999999 %}{{ i }}{% endfor %}", "123")]
    [InlineData("{% tablerow i in items limit: 99999999999 cols: 99999999999 %}{{ i }}{% endtablerow %}", "<tr class=\"row1\">\n<td class=\"col1\">1</td><td class=\"col2\">2</td><td class=\"col3\">3</td></tr>\n")]
    // The same with numeric strings, like Liquid.
    [InlineData("{% assign n = '99999999999' %}{% for i in items limit: n %}{{ i }}{% endfor %}|{% for i in items offset: n %}{{ i }}{% else %}none{% endfor %}", "123|none")]
    [InlineData("{% assign n = '-999999999999999999999999999999999' %}{% for i in items offset: n %}{{ i }}{% endfor %}|{% tablerow i in items limit: '99999999999' %}{{ i }}{% endtablerow %}", "123|<tr class=\"row1\">\n<td class=\"col1\">1</td><td class=\"col2\">2</td><td class=\"col3\">3</td></tr>\n")]
    public async Task HugeOffsetsLimitsAndLengthsMeanAllOrNone(string source, string expected)
    {
        Assert.Equal(expected, await RenderAsync(source));
    }

    [Theory]
    // Like Liquid, where integers are unbounded, a literal too large for a decimal is valid, and kept as its text.
    [InlineData("{{ 123456789012345678901234567890 }}|{{ -123456789012345678901234567890 }}", "123456789012345678901234567890|-123456789012345678901234567890")]
    [InlineData("{% assign x = 99999999999999999999999999999 %}{{ x }}|{{ 79228162514264337593543950335 | minus: 1 }}", "99999999999999999999999999999|79228162514264337593543950334")]
    public async Task IntegerLiteralsTooLargeForADecimalAreText(string source, string expected)
    {
        Assert.Equal(expected, await RenderAsync(source));
    }

    [Theory]
    [InlineData("{% for i in (1..99999999999999999999) %}{% endfor %}", "The range bound 99999999999999999999 is out of range.")]
    [InlineData("{% assign r = (-99999999999..1) %}", "The range (-99999999999..1) has too many items.")]
    [InlineData("{% for i in (-2000000000..2000000000) %}{% endfor %}", "The range (-2000000000..2000000000) has too many items.")]
    public async Task RangesTooLargeThrowALiquidException(string source, string message)
    {
        var exception = await Assert.ThrowsAsync<LiquidException>(() => RenderAsync(source));

        Assert.Equal(message, exception.Message);
    }

    [Theory]
    // Like Liquid, range bounds aren't limited to an int: a long holds any realistic bound.
    [InlineData("{{ (2147483648..2147483650) | join: ',' }}|{% for i in (2147483647..2147483648) %}{{ i }},{% endfor %}|{{ (2147483648..2) }}", "2147483648,2147483649,2147483650|2147483647,2147483648,|2147483648..2")]
    [InlineData("{% assign b = 3000000000 %}{% for i in (b..b) %}{{ i }}{{ forloop.name }}{% endfor %}|{% assign r = (b..3000000001) %}{{ r.first }}-{{ r.last }}-{{ r.size }}|{{ (-2147483649..-2147483648) | join: ',' }}", "3000000000i-(b..b)|3000000000-3000000001-2|-2147483649,-2147483648")]
    [InlineData("{{ (9223372036854775806..9223372036854775807) | join: ',' }}|{% if (3000000000..3000000001) contains 3000000001 %}T{% endif %}", "9223372036854775806,9223372036854775807|T")]
    public async Task RangesHoldLongBounds(string source, string expected)
    {
        Assert.Equal(expected, await RenderAsync(source));
    }

    [Theory]
    [InlineData(double.NaN, "NaN")]
    [InlineData(double.PositiveInfinity, "Infinity")]
    [InlineData(double.NegativeInfinity, "-Infinity")]
    [InlineData(double.MaxValue, "1.7976931348623157E+308")]
    [InlineData(1e30, "1E+30")]
    [InlineData(2.5, "2.5")]
    public async Task DoublesThatDontFitADecimalAreWrittenAsText(double value, string expected)
    {
        // A value set directly, a property and a field all go through a different conversion.
        var measurements = new Measurements { Ratio = value, Field = value };

        Assert.Equal(expected, await RenderAsync("{{ v }}", c => c.SetValue("v", value)));
        Assert.Equal(expected, await RenderAsync("{{ m.Ratio }}", c => c.SetValue("m", measurements)));
        Assert.Equal(expected, await RenderAsync("{{ m.Field }}", c => c.SetValue("m", measurements)));
    }

    [Fact]
    public async Task FloatsThatDontFitADecimalAreWrittenAsText()
    {
        var measurements = new Measurements { Single = float.PositiveInfinity };

        Assert.Equal("Infinity", await RenderAsync("{{ m.Single }}", c => c.SetValue("m", measurements)));
        Assert.Equal("NaN", await RenderAsync("{{ v }}", c => c.SetValue("v", float.NaN)));
    }
}
