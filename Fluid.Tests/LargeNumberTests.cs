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
    // A big literal is a sign, digits and an optional point followed by digits: the - of -}} and -%} isn't part of it.
    [InlineData("[{{99999999999999999999999999999-}} ]|{%- assign x = -99999999999999999999999999999-%} {{ x }}", "[99999999999999999999999999999]|-99999999999999999999999999999")]
    [InlineData("{{99999999999999999999999999999.25-}} |{{ 99999999999999999999999999999 | append: '' -}} |{{ x | default: 99999999999999999999999999999-}}", "1.0e+29|99999999999999999999999999999|99999999999999999999999999999")]
    public async Task BigNumberLiteralsEndBeforeWhitespaceControl(string source, string expected)
    {
        Assert.Equal(expected, await RenderAsync(source));
    }

    [Theory]
    // Like Liquid, where floats are doubles, a float literal too large for a decimal is valid, and written like a Ruby float.
    [InlineData("{{ 99999999999999999999999999999.0 }}|{{ -99999999999999999999999999999.0 }}|{% assign x = 79228162514264337593543950336.0 %}{{ x }}", "1.0e+29|-1.0e+29|7.922816251426434e+28")]
    [InlineData("{{ 123456789012345678901234567890.123456789 }}|{{ 100000000000000000000000000000000000000000000000000000000000000.0 }}|{{ 0099999999999999999999999999999 }}", "1.2345678901234568e+29|1.0e+62|99999999999999999999999999999")]
    [InlineData("{{ 99999999999999999999999999999 | json }}|{{ 99999999999999999999999999999.0 | json }}|{{ 99999999999999999999999999999.0 | append: '' | plus: 0 }}", "\"99999999999999999999999999999\"|1e+29|1")]
    // Like Shopify's json filter, a big integer is a string and a big float has no ".0".
    [InlineData("{{ -99999999999999999999999999999 | json }}|{{ 123456789012345678901234567890.123456789 | json }}|{{ -99999999999999999999999999999.0 | json }}", "\"-99999999999999999999999999999\"|1.2345678901234568e+29|-1e+29")]
    public async Task FloatLiteralsTooLargeForADecimalAreValid(string source, string expected)
    {
        Assert.Equal(expected, await RenderAsync(source));
    }

    [Theory]
    // Like Liquid, a number literal too large for a decimal is compared with other numbers by its sign and magnitude.
    [InlineData("{% if 99999999999999999999999999999 > 1 %}a{% endif %}{% if 1 < 99999999999999999999999999999 %}b{% endif %}{% if -99999999999999999999999999999 < 1 %}c{% endif %}{% if 1.5 <= 99999999999999999999999999999.0 %}d{% endif %}", "abcd")]
    [InlineData("{% if 99999999999999999999999999999 > 79228162514264337593543950335 %}a{% endif %}{% if 99999999999999999999999999999 > 99999999999999999999999999998 %}b{% endif %}{% if -99999999999999999999999999999 < -99999999999999999999999999998 %}c{% endif %}{% if 199999999999999999999999999998 >= 99999999999999999999999999999.0 %}d{% endif %}", "abcd")]
    [InlineData("{% assign x = 99999999999999999999999999999 %}{% if x > 5 and x >= 5 and 5 < x and 5 <= x %}ok{% endif %}|{% if 99999999999999999999999999999.0 >= 99999999999999999999999999999.0 %}ge{% endif %}", "ok|ge")]
    // Equality is exact: 99999999999999999999999999999.0 is the double 99999999999999991433150857216.
    [InlineData("{% if 99999999999999999999999999999 == 99999999999999999999999999999 %}a{% endif %}{% if 99999999999999999999999999999 == 99999999999999999999999999999.0 %}b{% endif %}{% if 79228162514264337593543950335 == 99999999999999999999999999999 %}c{% endif %}{% if 99999999999999999999999999999 == '99999999999999999999999999999' %}d{% endif %}", "a")]
    [InlineData("{% case 99999999999999999999999999999 %}{% when 99999999999999999999999999999 %}a{% endcase %}{% case 99999999999999999999999999999.0 %}{% when 100000000000000000000000000000.0 %}b{% endcase %}{% if 99999999999999999999999999999 contains '9' %}c{% endif %}", "ab")]
    public async Task NumberLiteralsTooLargeForADecimalAreCompared(string source, string expected)
    {
        Assert.Equal(expected, await RenderAsync(source));
    }

    [Theory]
    [InlineData("{% if 99999999999999999999999999999 > '1' %}{% endif %}", "comparison of Integer with String failed")]
    [InlineData("{% if '1' < 99999999999999999999999999999.0 %}{% endif %}", "comparison of String with Integer failed")]
    public async Task NumberLiteralsTooLargeForADecimalArentComparedWithStrings(string source, string message)
    {
        var exception = await Assert.ThrowsAsync<LiquidException>(() => RenderAsync(source));

        Assert.Equal(message, exception.Message);
    }

    [Theory]
    // Like Liquid, a length shorter than the ellipsis only keeps the ellipsis, even near int.MinValue.
    [InlineData("[{{ 'abcdef' | truncate: -2147483648 }}]|[{{ 'abcdef' | truncate: -9223372036854775808 }}]|[{{ 'abcdef' | truncate: -5 }}]", "[...]|[...]|[...]")]
    [InlineData("[{{ 'abcdef' | truncate: -2147483648, '' }}]|[{{ 'abcdef' | truncate: '-99999999999999999999999999999' }}]|[{{ 'abcdef' | truncate: 2, '' }}]", "[]|[...]|[ab]")]
    public async Task TruncateWithAHugeNegativeLengthKeepsTheEllipsis(string source, string expected)
    {
        var context = new TemplateContext(new TemplateOptionsBuilder().WithMaxOutputSize(1000).Build());

        Assert.Equal(expected, await _parser.Parse(source).RenderAsync(context));
    }

    [Theory]
    // Like Liquid, integer arguments are read like Ruby's Integer(), and saturated like numbers when out of an int's range.
    [InlineData("[{{ s | slice: '2147483648' }}]|[{{ s | slice: 1, '2147483648' }}]|[{{ items | slice: 0, '2147483648' | join }}]|[{{ s | slice: '-99999999999999999999999999999999' }}]", "[]|[ello World]|[1 2 3]|[]")]
    [InlineData("[{{ s | slice: 99999999999999999999999999999 }}]|[{{ s | slice: 0, 99999999999999999999999999999 }}]|[{{ s | truncate: '99999999999999999999999999999' }}]", "[]|[Hello World]|[Hello World]")]
    [InlineData("{% capture n %}2147483648{% endcapture %}[{{ s | slice: n }}]|{% for i in items limit: n %}{{ i }}{% endfor %}", "[]|123")]
    public async Task IntegerStringsOutOfIntRangeAreSaturated(string source, string expected)
    {
        Assert.Equal(expected, await RenderAsync(source, c => c.SetValue("s", "Hello World")));
    }

    [Theory]
    [InlineData("{% for i in (1..99999999999999999999) %}{% endfor %}", "The range bound 99999999999999999999 is out of range.")]
    // A literal bound too large for a decimal parses, like in Liquid, and is out of range too.
    [InlineData("{% for i in (99999999999999999999999999998..99999999999999999999999999999) %}{{ i }},{% endfor %}", "The range bound 99999999999999999999999999998 is out of range.")]
    [InlineData("{% assign r = (1..99999999999999999999999999999) %}[{{ r }}]", "The range bound 99999999999999999999999999999 is out of range.")]
    [InlineData("{% assign r = (1 .. -99999999999999999999999999999.5) %}", "The range bound -99999999999999999999999999999 is out of range.")]
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
