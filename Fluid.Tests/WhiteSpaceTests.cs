using System;
using System.Threading.Tasks;
using Xunit;

namespace Fluid.Tests;

public class WhiteSpaceTests
{
    private static FluidParser _parser = new FluidParser();

    [Fact]
    public async Task ShouldPreserveSpace()
    {
        var source = @"# With whitespace control
{% assign name = 'John G. Chalmers-Smith' %}
{% if name and name.size > 10 %}
    Wow, {{ name }}, you have a long name!
{% else %}
    Hello there!
{% endif %}";

        var expected = @"# With whitespace control


    Wow, John G. Chalmers-Smith, you have a long name!
";

        _parser.TryParse(source, out var template);
        var result = await template.RenderAsync();

        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task ShouldStripSpace()
    {
        var source = @"# With whitespace control
{% assign name = 'John G. Chalmers-Smith' -%}
{%- if name and name.size > 10 -%}
    Wow, {{ name }}, you have a long name!
{%- else -%}
    Hello there!
{%- endif -%}";

        var expected = @"# With whitespace control
Wow, John G. Chalmers-Smith, you have a long name!";

        _parser.TryParse(source, out var template);
        var result = await template.RenderAsync();

        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task ShouldRenderSampleWithDashes()
    {
        var sample = @"
<ul id=""products"">
  {%- for product in products %}
  <li>
    <h2>{{ product.name }}</h2>
    Only {{ product.price | price }}

    {{ product.name | prettyprint | paragraph }}
  </li>
  {%- endfor %}
</ul>
";

        var expected = @"
<ul id=""products"">
  <li>
    <h2>product 1</h2>
    Only 1

    product 1
  </li>
  <li>
    <h2>product 2</h2>
    Only 2

    product 2
  </li>
  <li>
    <h2>product 3</h2>
    Only 3

    product 3
  </li>
</ul>
";

        var _products = new[]
        {
            new { name = "product 1", price = 1 },
            new { name = "product 2", price = 2 },
            new { name = "product 3", price = 3 },
        };

        _parser.TryParse(sample, out var template, out var messages);

        var options = new TemplateOptionsBuilder()
            .AddFilter("prettyprint", (input, args, ctx) => input)
            .AddFilter("paragraph", (input, args, ctx) => input)
            .AddFilter("price", (input, args, ctx) => input)
            .Build();
        var context = new TemplateContext(options);
        context.SetValue("products", _products);

        var result = await template.RenderAsync(context);
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task ShouldRenderSampleWithTrimLeftTag()
    {
        var sample = @"
<ul id=""products"">
  {% for product in products %}
  <li>
    <h2>{{ product.name }}</h2>
    Only {{ product.price | price }}

    {{ product.name | prettyprint | paragraph }}
  </li>
  {% endfor %}
</ul>
";

        var expected = @"
<ul id=""products"">
  <li>
    <h2>product 1</h2>
    Only 1

    product 1
  </li>
  <li>
    <h2>product 2</h2>
    Only 2

    product 2
  </li>
  <li>
    <h2>product 3</h2>
    Only 3

    product 3
  </li>
</ul>
";

        var _products = new[]
        {
            new { name = "product 1", price = 1 },
            new { name = "product 2", price = 2 },
            new { name = "product 3", price = 3 },
        };

        _parser.TryParse(sample, out var template, out var messages);

        var options = new TemplateOptionsBuilder()
            .WithTrimming(TrimmingFlags.TagLeft)
            .AddFilter("prettyprint", (input, args, ctx) => input)
            .AddFilter("paragraph", (input, args, ctx) => input)
            .AddFilter("price", (input, args, ctx) => input)
            .Build();
        var context = new TemplateContext(options);
        context.SetValue("products", _products);

        var result = await template.RenderAsync(context);
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task ShouldRenderSampleWithDashesRight()
    {
        var sample = @"
<ul id=""products"">
  {% for product in products -%}
  <li>
    <h2>{{ product.name }}</h2>
    Only {{ product.price | price }}

    {{ product.name | prettyprint | paragraph }}
  </li>
  {% endfor -%}
</ul>
";

        var expected = @"
<ul id=""products"">
  <li>
    <h2>product 1</h2>
    Only 1

    product 1
  </li>
  <li>
    <h2>product 2</h2>
    Only 2

    product 2
  </li>
  <li>
    <h2>product 3</h2>
    Only 3

    product 3
  </li>
  </ul>
";

        var _products = new[]
        {
            new { name = "product 1", price = 1 },
            new { name = "product 2", price = 2 },
            new { name = "product 3", price = 3 },
        };

        _parser.TryParse(sample, out var template, out var messages);

        var options = new TemplateOptionsBuilder()
            .AddFilter("prettyprint", (input, args, ctx) => input)
            .AddFilter("paragraph", (input, args, ctx) => input)
            .AddFilter("price", (input, args, ctx) => input)
            .Build();
        var context = new TemplateContext(options);
        context.SetValue("products", _products);

        var result = await template.RenderAsync(context);
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task ShouldRenderSampleWithTrimTagRight()
    {
        var sample = @"
<ul id=""products"">
  {% for product in products %}
  <li>
    <h2>{{ product.name }}</h2>
    Only {{ product.price | price }}

    {{ product.name | prettyprint | paragraph }}
  </li>
  {% endfor %}
</ul>
";

        var expected = @"
<ul id=""products"">
  <li>
    <h2>product 1</h2>
    Only 1

    product 1
  </li>
  <li>
    <h2>product 2</h2>
    Only 2

    product 2
  </li>
  <li>
    <h2>product 3</h2>
    Only 3

    product 3
  </li>
  </ul>
";

        var _products = new[]
        {
            new { name = "product 1", price = 1 },
            new { name = "product 2", price = 2 },
            new { name = "product 3", price = 3 },
        };

        _parser.TryParse(sample, out var template, out var messages);

        var options = new TemplateOptionsBuilder()
            .WithTrimming(TrimmingFlags.TagRight)
            .AddFilter("prettyprint", (input, args, ctx) => input)
            .AddFilter("paragraph", (input, args, ctx) => input)
            .AddFilter("price", (input, args, ctx) => input)
            .Build();
        var context = new TemplateContext(options);
        context.SetValue("products", _products);

        var result = await template.RenderAsync(context);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("{% for i in (1..3) %} Hi! {{ i }} {% endfor %}", " Hi! 1  Hi! 2  Hi! 3 ")]
    [InlineData("{{'a'}} {% for i in (1..3) %}{{ i }}{% endfor %}", "a 123")]
    [InlineData("{% for i in (1..3) %} {{ i }}{% endfor %}", " 1 2 3")]
    [InlineData("{% for i in (1..3) %}{{ i }} {% endfor %}", "1 2 3 ")]
    [InlineData("{% for i in (1..3) %} {{ i }} {% endfor %}", " 1  2  3 ")]
    public async Task BlockShouldMaintainWhiteSpaceWhenNotEmpty(string source, string expected)
    {

        _parser.TryParse(source, out var template, out var messages);
        var result = await template.RenderAsync();

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(" {{ 1 }} ", " 1 ")]
    [InlineData(" {{ 1 }} \n", " 1 \n")]
    [InlineData(" {{ 1 }} \n ", " 1 \n ")]
    public async Task ShouldNotTrimOutputTag(string source, string expected)
    {
        var success = _parser.TryParse(source, out var template, out var messages);
        Assert.True(success, String.Join(", ", messages));
        var result = await template.RenderAsync();

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(" {{- 1 }} ", "1 ")]
    [InlineData(" {{ 1 -}} ", " 1")]
    [InlineData(" {{ 1 -}} \n", " 1")]
    [InlineData(" {{ 1 -}} \n ", " 1")]
    [InlineData(" {{ 1 -}} \n\n ", " 1")]
    [InlineData(" {{ 1 -}} \r\n ", " 1")]
    [InlineData(" {{ 1 -}} \r\n\r\n ", " 1")]
    [InlineData("a {{ 1 }}", "a 1")]
    [InlineData("a {% assign a = '' %}", "a ")]
    [InlineData("1<div class=\"a{% if true %}b{% endif %}\" />", "1<div class=\"ab\" />")]
    [InlineData("2<div class=\"a {% if true %} b {% endif %}\" />", "2<div class=\"a  b \" />")]
    [InlineData("3<div class=\"a{%- if true -%}b{%- endif %}\" />", "3<div class=\"ab\" />")]
    [InlineData("4<div class=\"a {%- if true -%} b {%- endif %}\" />", "4<div class=\"ab\" />")]
    [InlineData("5<div class=\"a {%- if true %} b {%- endif %}\" />", "5<div class=\"a b\" />")]
    [InlineData("6<div class=\"a{% if true %} b{% endif %}\" />", "6<div class=\"a b\" />")]
    public async Task DashShouldTrimOutputTag(string source, string expected)
    {
        var success = _parser.TryParse(source, out var template, out var messages);
        Assert.True(success, String.Join(", ", messages));
        var result = await template.RenderAsync();

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(" {{ 1 }} ", " 1")]
    public async Task ShouldTrimOutputRight(string source, string expected)
    {
        var success = _parser.TryParse(source, out var template, out var messages);
        Assert.True(success, String.Join(", ", messages));

        var options = new TemplateOptionsBuilder().WithTrimming(TrimmingFlags.OutputRight).Build();
        var context = new TemplateContext(options);
        var result = await template.RenderAsync(context);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(" {{ 1 }} ", "1 ")]
    public async Task ShouldTrimOutputLeft(string source, string expected)
    {
        var success = _parser.TryParse(source, out var template, out var messages);
        Assert.True(success, String.Join(", ", messages));

        var options = new TemplateOptionsBuilder().WithTrimming(TrimmingFlags.OutputLeft).Build();
        var context = new TemplateContext(options);
        var result = await template.RenderAsync(context);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ShouldParseNonBreakingWhitespace()
    {
        var c = (char)160;

        Assert.True(_parser.TryParse("{{" + c + "'a'" + c + "}}", out var template, out var errors));
        Assert.Equal("a", template.Render());
    }

    [Theory]
    [InlineData("  {{- 1 }}  ", "1  ")]
    [InlineData("  {{ 1 -}}  ", "  1")]
    [InlineData("  {%- assign a = 1 %}  ", "  ")]
    [InlineData("  {% assign a = 1 -%}  ", "  ")]

    [InlineData("  {{- 1 }}  \r\n", "1  \r\n")]
    [InlineData("  {{- 1 }}  \n", "1  \n")]
    [InlineData("\r\n  {{- 1 }}  ", "\r\n1  ")]
    [InlineData("\n  {{- 1 }}  ", "\n1  ")]
    [InlineData("\r\n\r\n  {{- 1 }}  ", "\r\n\r\n1  ")]
    [InlineData("\n\n  {{- 1 }}  ", "\n\n1  ")]

    [InlineData("\r\n  {{ 1 -}}  ", "\r\n  1")]
    [InlineData("\n  {{ 1 -}}  ", "\n  1")]
    [InlineData("  {{ 1 -}}  \r\n", "  1")]
    [InlineData("  {{ 1 -}}  \n", "  1")]
    [InlineData("  {{ 1 -}}  \r\n\r\n", "  1\r\n")]
    [InlineData("  {{ 1 -}}  \n\n", "  1\n")]
    public async Task NotGreedyShouldStripUntilFirstLine(string source, string expected)
    {
        var success = _parser.TryParse(source, out var template, out var messages);
        Assert.True(success, String.Join(", ", messages));

        var options = new TemplateOptionsBuilder().WithGreedy(false).Build();
        var context = new TemplateContext(options);
        var result = await template.RenderAsync(context);

        Assert.Equal(expected, result);
    }

    // Both parser modes skip Parlot's whitespace in tags and outputs: the chars of Ruby's \s, which are the only ones
    // Shopify skips, and the Unicode spaces (Fluid accepts a non-breaking space since #138).
    private const string TagWhiteSpace = " \t\n\v\f\r\u00A0\u1680\u180E\u2000\u2007\u200A\u202F\u205F\u3000\uFEFF";
    private const string NotTagWhiteSpace = "\u0085\u200B\u2028\u2029";

    public static TheoryData<int, bool> TagWhiteSpaceChars()
    {
        var data = new TheoryData<int, bool>();
        foreach (var c in TagWhiteSpace) data.Add(c, true);
        foreach (var c in NotTagWhiteSpace) data.Add(c, false);
        return data;
    }

    [Theory]
    [MemberData(nameof(TagWhiteSpaceChars))]
    public void ShouldSkipTheSameWhiteSpaceInTagsAndOutputsInBothModes(int code, bool isWhiteSpace)
    {
        var s = ((char)code).ToString();

        string[] sources =
        [
            "{% assign x = 'ok' %}[{{ x" + s + "}}]",
            "{% assign x = 'ok' %}[{{ x" + s + "-}}]",
            "{% assign x = 'ok' %}[{{" + s + "x }}]",
            "{% assign x = 'ok' %}[{{ x" + s + "|" + s + "downcase }}]",
            "[{% if true" + s + "%}ok{% endif %}]",
            "[{%" + s + "if true %}ok{% endif %}]",
            "[{% if" + s + "true %}ok{% endif %}]",
            "[{% if 1" + s + "==" + s + "1 %}ok{% endif %}]",
            "[{% if true %}ok{%" + s + "endif" + s + "%}]",
        ];

        foreach (var allowLiquidTag in new[] { false, true })
        {
            var parser = new FluidParser(new FluidParserOptions { AllowLiquidTag = allowLiquidTag });

            foreach (var source in sources)
            {
                Assert.True(isWhiteSpace == parser.TryParse(source, out var template, out var errors), $"AllowLiquidTag={allowLiquidTag} {source} {errors}");

                if (isWhiteSpace)
                {
                    Assert.Equal("[ok]", template.Render());
                }
            }
        }
    }

    // Like Shopify, the whitespace control only trims the chars of Ruby's strip, and a text is only blank with the chars
    // of Ruby's \s: the other spaces are content.
    private const string UnicodeSpaces = "\u0085\u00A0\u1680\u180E\u2000\u2007\u200A\u200B\u2028\u2029\u202F\u205F\u3000\uFEFF";

    public static TheoryData<int, bool> TrimmedChars()
    {
        var data = new TheoryData<int, bool>();
        foreach (var c in " \t\n\v\f\r\0") data.Add(c, true);
        foreach (var c in UnicodeSpaces) data.Add(c, false);
        return data;
    }

    public static TheoryData<int, bool> BlankChars()
    {
        var data = new TheoryData<int, bool>();
        foreach (var c in " \t\n\v\f\r") data.Add(c, true);
        foreach (var c in "\0" + UnicodeSpaces) data.Add(c, false);
        return data;
    }

    [Theory]
    [MemberData(nameof(TrimmedChars))]
    public void WhitespaceControlShouldOnlyTrimLiquidWhiteSpace(int code, bool isTrimmed)
    {
        var s = ((char)code).ToString();
        var expected = isTrimmed ? "[x]" : "[" + s + "x" + s + "]";

        Assert.Equal(expected, _parser.Parse("[" + s + "{{- 'x' -}}" + s + "]").Render());
        Assert.Equal(expected, _parser.Parse("[" + s + "{%- if true -%}x{%- endif -%}" + s + "]").Render());

        var options = new TemplateOptionsBuilder().WithTrimming(TrimmingFlags.OutputLeft | TrimmingFlags.OutputRight).Build();
        Assert.Equal(expected, _parser.Parse("[" + s + "{{ 'x' }}" + s + "]").Render(new TemplateContext(options)));
    }

    [Theory]
    [MemberData(nameof(BlankChars))]
    public void BlockShouldOnlyBeBlankWithLiquidWhiteSpace(int code, bool isBlank)
    {
        var s = ((char)code).ToString();
        var expected = isBlank ? "[]" : "[" + s + s + "]";

        Assert.Equal(expected, _parser.Parse("[{% if true %}" + s + s + "{% endif %}]").Render());
        Assert.Equal(expected, _parser.Parse("[{% unless false %}" + s + "{% assign a = 1 %}" + s + "{% endunless %}]").Render());
    }
}
