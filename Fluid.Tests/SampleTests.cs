using System.Collections.Generic;
using System.Linq;
using Fluid.Ast;
using Irony.Parsing;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Fluid.Tests
{
    public class SampleTests
    {
        private static LanguageData _language = new LanguageData(new FluidGrammar());

        private IList<Statement> Parse(string template)
        {
            new FluidParser().TryParse(new StringSegment(template), out var statements, out var errors);
            return statements;
        }

        [Fact]
        public void ShouldRenderSample1()
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
            context.Filters.Add("prettyprint", (input, args) => input);
            context.Filters.Add("paragraph", (input, args) => input);
            context.Filters.Add("price", (input, args) => input);

            var result = template.Render(context);
            Assert.Equal(expected, result);
        }        
    }
}
