using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Fluid.Ast;
using Fluid.Values;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Fluid.Tests
{
    public class CycleStatementTests
    {
        private Statement[] TEXT(string text)
        {
            return new Statement[] { new TextStatement(new StringSegment(text)) };
        }

        private LiteralExpression LIT(string text)
        {
            return new LiteralExpression(new StringValue(text));
        }

        [Fact]
        public async Task CycleEvaluatesEachValue()
        {
            var e = new CycleStatement(
                null,
                new[] {
                    LIT("a"), LIT("b"), LIT("c")
                    }
                );

            var context = new TemplateContext();

            var sw = new StringWriter();
            for (var i=1; i<=10; i++)
            {
                await e.WriteToAsync(sw, HtmlEncoder.Default, context);
            }

            Assert.Equal("abcabcabca", sw.ToString());
        }

        [Fact]
        public async Task CycleEvaluatesGroupsValue()
        {
            var group1 = new CycleStatement(
                LIT("x"),
                new[] {
                    LIT("a"), LIT("b"), LIT("c")
                    }
                );

            var group2 = new CycleStatement(
                LIT("y"),
                new[] {
                    LIT("a"), LIT("b"), LIT("c")
                    }
                );

            var context = new TemplateContext();

            var sw = new StringWriter();
            for (var i = 1; i <= 5; i++)
            {
                await group1.WriteToAsync(sw, HtmlEncoder.Default, context);
                await group2.WriteToAsync(sw, HtmlEncoder.Default, context);
            }

            Assert.Equal("aabbccaabb", sw.ToString());
        }

        [Fact]
        public async Task CycleAlternateValuesList()
        {
            var group1 = new CycleStatement(
                null,
                new[] {
                    LIT("a"), LIT("b"), LIT("c")
                    }
                );

            var group2 = new CycleStatement(
                null,
                new[] {
                    LIT("d"), LIT("e"), LIT("f")
                    }
                );

            var context = new TemplateContext();

            var sw = new StringWriter();
            for (var i = 1; i <= 5; i++)
            {
                await group1.WriteToAsync(sw, HtmlEncoder.Default, context);
                await group2.WriteToAsync(sw, HtmlEncoder.Default, context);
            }

            Assert.Equal("aecdbfaecd", sw.ToString());
        }
    }
}
