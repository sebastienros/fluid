using System.IO;
using System.Text.Encodings.Web;
using Fluid.Ast;
using Fluid.Values;
using Xunit;

namespace Fluid.Tests
{
    public class UnlessStatementTests
    {
        private Expression TRUE = new LiteralExpression(new BooleanValue(true));
        private Expression FALSE = new LiteralExpression(new BooleanValue(false));

        private Statement[] TEXT(string text)
        {
            return new Statement[] { new TextStatement(text) };
        }

        [Fact]
        public void UnlessDoesntProcessWhenTrue()
        {
            var e = new UnlessStatement(
                TRUE,
                new[] { new TextStatement("x") }
                );

            var sw = new StringWriter();
            e.WriteTo(sw, HtmlEncoder.Default, new TemplateContext());

            Assert.Equal("", sw.ToString());
        }

        [Fact]
        public void IfCanProcessWhenFalse()
        {
            var e = new UnlessStatement(
                FALSE,
                new[] { new TextStatement("x") }
                );

            var sw = new StringWriter();
            e.WriteTo(sw, HtmlEncoder.Default, new TemplateContext());

            Assert.Equal("x", sw.ToString());
        }
    }
}
