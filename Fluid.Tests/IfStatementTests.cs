using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using Fluid.Ast;
using Fluid.Ast.Values;
using Xunit;

namespace Fluid.Tests
{
    public class IfStatementTests
    {
        [Fact]
        public void IfCanProcessWhenTrue()
        {
            var e = new IfStatement(
                new LiteralExpression(new BooleanValue(true)),
                new[] { new TextStatement("x") }
                );

            var sw = new StringWriter();
            e.WriteTo(sw, HtmlEncoder.Default, new TemplateContext());

            Assert.Equal("x", sw.ToString());
        }

        [Fact]
        public void IfDoesntProcessWhenFalse()
        {
            var e = new IfStatement(
                new LiteralExpression(new BooleanValue(false)),
                new[] { new TextStatement("x") }
                );

            var sw = new StringWriter();
            e.WriteTo(sw, HtmlEncoder.Default, new TemplateContext());

            Assert.Equal("", sw.ToString());
        }

        [Fact]
        public void IfDoesntProcessElseWhenTrue()
        {
            var e = new IfStatement(
                new LiteralExpression(new BooleanValue(true)),
                new Statement[] {
                    new TextStatement("x")
                },
                new ElseStatement(new[] {
                        new TextStatement("y")
                    }));

            var sw = new StringWriter();
            e.WriteTo(sw, HtmlEncoder.Default, new TemplateContext());

            Assert.Equal("x", sw.ToString());
        }

        [Fact]
        public void IfProcessElseWhenFalse()
        {
            var e = new IfStatement(
                new LiteralExpression(new BooleanValue(false)),
                new Statement[] {
                    new TextStatement("x")
                },
                new ElseStatement(new[] {
                        new TextStatement("y")
                    })
                );

            var sw = new StringWriter();
            e.WriteTo(sw, HtmlEncoder.Default, new TemplateContext());

            Assert.Equal("y", sw.ToString());
        }
    }
}
