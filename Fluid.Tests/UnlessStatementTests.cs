using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Fluid.Ast;
using Fluid.Values;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Fluid.Tests
{
    public class UnlessStatementTests
    {
        private Expression TRUE = new LiteralExpression(BooleanValue.True);
        private Expression FALSE = new LiteralExpression(BooleanValue.False);

        private Statement[] TEXT(string text)
        {
            return new Statement[] { new TextSpanStatement(text) };
        }

        [Fact]
        public async Task UnlessDoesntProcessWhenTrue()
        {
            var e = new UnlessStatement(
                TRUE,
                new List<Statement> { new TextSpanStatement("x") }
                );

            var sw = new StringWriter();
            await e.WriteToAsync(sw, HtmlEncoder.Default, new TemplateContext());

            Assert.Equal("", sw.ToString());
        }

        [Fact]
        public async Task UnlessCanProcessWhenFalse()
        {
            var e = new UnlessStatement(
                FALSE,
                new List<Statement> { new TextSpanStatement("x") }
                );

            var sw = new StringWriter();
            await e.WriteToAsync(sw, HtmlEncoder.Default, new TemplateContext());

            Assert.Equal("x", sw.ToString());
        }

        [Fact]
        public async Task UnlessShouldProcessElseWhenFalse()
        {
            var e = new UnlessStatement(
                FALSE,
                new List<Statement> {
                    new TextSpanStatement("x")
                },
                new ElseStatement(new List<Statement> {
                        new TextSpanStatement("y")
                    }));

            var sw = new StringWriter();
            await e.WriteToAsync(sw, HtmlEncoder.Default, new TemplateContext());

            Assert.Equal("x", sw.ToString());
        }

        [Fact]
        public async Task UnlessShouldProcessElseWhenTrue()
        {
            var e = new UnlessStatement(
                TRUE,
                new List<Statement> {
                    new TextSpanStatement("x")
                },
                new ElseStatement(new List<Statement> {
                        new TextSpanStatement("y")
                    })
                );

            var sw = new StringWriter();
            await e.WriteToAsync(sw, HtmlEncoder.Default, new TemplateContext());

            Assert.Equal("y", sw.ToString());
        }
    }
}
