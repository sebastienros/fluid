using Fluid.Ast;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Fluid.Tests
{
    public class WhiteSpaceTests
    {
        private static IFluidParser _parser = new FluidParser();

        private IReadOnlyList<Statement> Parse(string source)
        {
            _parser.TryParse(source, out var template, out var errors);
            return template.Statements;
        }

        [Fact]
        public async Task ShouldRenderSampleWithStandardLiquid()
        {
            var sample = @"
<ul id=""products"">
  {%- for product in products -%}
    <li>
      <h2>{{ product.name }}</h2>
      Only {{ product.price | price }}

      {{ product.name | prettyprint | paragraph }}
    </li>
  {%- endfor -%}
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

            var context = new TemplateContext();
            context.SetValue("products", _products);
            context.Filters.AddFilter("prettyprint", (input, args, ctx) => input);
            context.Filters.AddFilter("paragraph", (input, args, ctx) => input);
            context.Filters.AddFilter("price", (input, args, ctx) => input);
            context.MemberAccessStrategy.Register(new { name = "", price = 0 }.GetType());

            var result = await template.RenderAsync(context);
            Assert.Equal(expected, result);
        }

        [Fact]
        public async Task ShouldRenderSampleWithStandardLiquidAndNoStripEmptyLines()
        {
            var sample = @"
<ul id=""products"">
  {%- for product in products -%}
    <li>
      <h2>{{ product.name }}</h2>
      Only {{ product.price | price }}

      {{ product.name | prettyprint | paragraph }}
    </li>
  {%- endfor -%}
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

            var context = new TemplateContext();
            context.SetValue("products", _products);
            context.Filters.AddFilter("prettyprint", (input, args, ctx) => input);
            context.Filters.AddFilter("paragraph", (input, args, ctx) => input);
            context.Filters.AddFilter("price", (input, args, ctx) => input);
            context.MemberAccessStrategy.Register(new { name = "", price = 0 }.GetType());

            var result = await template.RenderAsync(context);
            Assert.Equal(expected, result);
        }

        [Fact]
        public async Task ShouldRenderSampleWithDashes()
        {
            var sample = @"
<ul id=""products"">
  {%- for product in products -%}
    <li>
      <h2>{{ product.name }}</h2>
      Only {{ product.price | price }}

      {{ product.name | prettyprint | paragraph }}
    </li>
  {%- endfor -%}
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

            var context = new TemplateContext();
            context.SetValue("products", _products);
            context.Filters.AddFilter("prettyprint", (input, args, ctx) => input);
            context.Filters.AddFilter("paragraph", (input, args, ctx) => input);
            context.Filters.AddFilter("price", (input, args, ctx) => input);
            context.MemberAccessStrategy.Register(new { name = "", price = 0 }.GetType());

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
        [InlineData(" {{ 1 -}} \n ", " 1 ")]
        [InlineData(" {{ 1 -}} \n\n ", " 1\n ")]
        [InlineData(" {{ 1 -}} \r\n ", " 1 ")]
        [InlineData(" {{ 1 -}} \r\n\r\n ", " 1\r\n ")]
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

        [Fact]
        public void ShouldParseNonBreakingWhitespace()
        {
            var c = (char)160;

            var result = _parser.TryParse("{{" + c + "'a'" + c + "}}", out var template, out var errors);
            var rendered = template.Render();

            Assert.Equal("a", rendered);
        }
    }
}
