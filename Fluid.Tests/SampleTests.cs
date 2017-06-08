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

        private List<Statement> Parse(string template)
        {
            new FluidParser().TryParse(new StringSegment(template), out var statements, out var errors);
            return statements;
        }

        [Fact]
        public void ShouldParseSample1()
        {
            var sample = @"
<ul id=""products"">
  {% for product in products %}
    <li>
      <h2>{{ product.name }}</h2>
      Only {{ product.price | price }}

      {{ product.description | prettyprint | paragraph }}
    </li>
  {% endfor %}
</ul>
";

            var statements = Parse(sample);

            Assert.Equal(3, statements.Count);

            var forStatement = statements.ElementAt(1) as ForStatement;
            Assert.Equal(7, forStatement.Statements.Count);
        }
        
    }
}
