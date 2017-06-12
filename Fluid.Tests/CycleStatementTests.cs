using System.IO;
using System.Text.Encodings.Web;
using Fluid.Ast;
using Fluid.Values;
using Xunit;

namespace Fluid.Tests
{
    public class CycleStatementTests
    {
        private Statement[] TEXT(string text)
        {
            return new Statement[] { new TextStatement(text) };
        }

        private LiteralExpression LIT(string text)
        {
            return new LiteralExpression(new StringValue(text));
        }

        [Fact]
        public void CycleEvaluatesEachValue()
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
                e.WriteTo(sw, HtmlEncoder.Default, context);
            }

            Assert.Equal("abcabcabca", sw.ToString());
        }

        [Fact]
        public void CycleEvaluatesGroupsValue()
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
                group1.WriteTo(sw, HtmlEncoder.Default, context);
                group2.WriteTo(sw, HtmlEncoder.Default, context);
            }

            Assert.Equal("aabbccaabb", sw.ToString());
        }

        [Fact]
        public void CycleAlternateValuesList()
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
                group1.WriteTo(sw, HtmlEncoder.Default, context);
                group2.WriteTo(sw, HtmlEncoder.Default, context);
            }

            Assert.Equal("aecdbfaecd", sw.ToString());
        }
    }
}
