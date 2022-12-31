using Fluid.Tests.Visitors;
using Fluid.Values;
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
        public void ShouldSubstract()
        {
            var template = new FluidParser().Parse("{{ 1 | plus: 2 }}");
            var visitor = new ReplacePlustFiltersVisitor();
            var changed = visitor.VisitTemplate(template);

            var result = changed.Render();

            Assert.Equal("-1", result);
        }


        [Fact]
        public void ShouldDoNothing()
        {
            var template = new FluidParser().Parse("{{ 1 | plus: 2 }}");
            var visitor = new RemovePlustFiltersVisitor();
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
    }
}
