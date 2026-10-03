using Fluid.Ast;
using Fluid.Tests.Visitors;
using Fluid.Values;
using Fluid.ViewEngine;
using Parlot.Fluent;
using Xunit;

namespace Fluid.Tests
{
    public class VisitorTest
    {
        [Fact]
        public void ShouldReplaceTwos()
        {
            var template = new FluidParser().Parse("{{ 1 | plus: 2 }}");
            var visitor = new ReplaceTwosVisitor(NumberValue.Create(4));
            var changed = visitor.VisitTemplate(template);

            var result = changed.Render();

            Assert.Equal("5", result);
        }

        [Fact]
        public void ShouldSubtract()
        {
            var template = new FluidParser().Parse("{{ 1 | plus: 2 }}");
            var visitor = new ReplacePlusFiltersVisitor();
            var changed = visitor.VisitTemplate(template);

            var result = changed.Render();

            Assert.Equal("-1", result);
        }


        [Fact]
        public void ShouldDoNothing()
        {
            var template = new FluidParser().Parse("{{ 1 | plus: 2 }}");
            var visitor = new RemovePlusFiltersVisitor();
            var changed = visitor.VisitTemplate(template);

            var result = changed.Render();

            Assert.Equal("1", result);
        }

        [Fact]
        public void ShouldDetectForLoopUsage()
        {
            var template1 = new FluidParser().Parse(@"
                {% for page in pages -%}
                  {%- if forloop.length > 0 -%}
                    {{ page.title }}{% unless forloop.last %}, {% endunless -%}
                  {%- endif -%}
                {% endfor %}"
            );

            var template2 = new FluidParser().Parse(@"
                {% for page in pages -%}
                  {%- if pages.length > 0 -%}
                    {{ page.title }}{% unless pages.last %}, {% endunless -%}
                  {%- endif -%}
                {% endfor %}"
            );

            var visitor = new IdentifierIsAccessedVisitor("forloop");

            visitor.VisitTemplate(template1);
            var result1 = visitor.IsAccessed;

            visitor.VisitTemplate(template2);
            var result2 = visitor.IsAccessed;

            Assert.True(result1);
            Assert.False(result2);
        }

        [Fact]
        public void VisitorShouldVisitParserTag()
        {
            var template = new FluidViewParser().Parse("{% layout '_Layout' %}");
        
            var visitor = new ParserVisitor();
            visitor.VisitTemplate(template);

            Assert.Equal("layout", visitor.TagName);
            Assert.IsType<LiteralExpression>(visitor.Value);
        }

        [Fact]
        public void VisitorShouldVisitParserBlock()
        {
            var template = new FluidViewParser().Parse("{% section body %}HELLO{% endsection %}");

            var visitor = new ParserVisitor();
            visitor.VisitTemplate(template);

            Assert.Equal("section", visitor.TagName);
            Assert.IsType<string>(visitor.Value);
            Assert.Single(visitor.Statements);
        }

        [Fact]
        public void VisitorShouldVisitEmptyTag()
        {
            var template = new FluidViewParser().Parse("{% renderbody %}");

            var visitor = new ParserVisitor();
            visitor.VisitTemplate(template);

            Assert.Equal("renderbody", visitor.TagName);
        }

        [Fact]
        public void VisitorShouldVisitEmptyBlock()
        {
            var parser = new FluidParser();

            parser.RegisterEmptyBlock("hello", static (s, w, e, c) =>
            {
                w.Write("Hello World");
                return s.RenderStatementsAsync(w, e, c);
            });

            var template = parser.Parse("{% hello %}HELLO{% endhello %}");

            var visitor = new ParserVisitor();
            visitor.VisitTemplate(template);

            Assert.Equal("hello", visitor.TagName);
            Assert.Single(visitor.Statements);
        }
    }
}
