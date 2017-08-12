using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fluid.Ast;
using Xunit;

namespace Fluid.Tests
{
    public class WhiteSpaceTests
    {
        private IList<Statement> Parse(string source)
        {
            FluidTemplate.TryParse(source, out var template, out var errors);
            return template.Statements;
        }

        [Fact]
        public async Task ShouldRenderSample()
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

            FluidTemplate.TryParse(sample, out var template, out var messages);

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

            FluidTemplate.TryParse(sample, out var template, out var messages);

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
        public async Task EndBlockShouldMaintainWhiteSpaceWhenNotEmpty()
        {
            var source = "{% for i in (1..3) %} Hi! {{ i }} {% endfor %}";
            var expected = " Hi! 1  Hi! 2  Hi! 3 ";

            FluidTemplate.TryParse(source, out var template, out var messages);
            var result = await template.RenderAsync();

            Assert.Equal(expected, result);
        }


        [Fact]
        public async Task EndBlockShouldMaintainWhiteSpaceWhenNotEmpty2()
        {
            var source = "{{'a'}} {% for i in (1..3) %}{{ i }}{% endfor %}";
            var expected = "a 123";

            FluidTemplate.TryParse(source, out var template, out var messages);
            var result = await template.RenderAsync();

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(" {{ 1 }} ", " 1 ")]
        [InlineData(" {{ 1 }} \n", " 1 \n")]
        [InlineData(" {{ 1 }} \n ", " 1 \n ")]
        public async Task ShouldNotTrimOutputTag(string source, string expected)
        {
            var success = FluidTemplate.TryParse(source, out var template, out var messages);
            Assert.True(success, String.Join(", ", messages));
            var result = await template.RenderAsync();

            Assert.Equal(expected, result);
        }

        [Theory]
        //[InlineData(" {{- 1 }} ", "1 ")]
        [InlineData(" {{ 1 -}} ", " 1")]
        //[InlineData(" {{ 1 -}} \n", " 1")]
        //[InlineData(" {{ 1 -}} \n ", " 1 ")]
        public async Task DashShouldTrimOutputTag(string source, string expected)
        {
            var success = FluidTemplate.TryParse(source, out var template, out var messages);
            Assert.True(success, String.Join(", ", messages));
            var result = await template.RenderAsync();

            Assert.Equal(expected, result);
        }
    }
}
