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
    public class ForStatementTests
    {
        [Fact]
        public void ShouldLoopRange()
        {
            var e = new ForStatement(
                new[] { new TextStatement("x") },
                "i",
                new RangeExpression(
                    new LiteralExpression(new NumberValue(1)),
                    new LiteralExpression(new NumberValue(3))
                )
            );

            var sw = new StringWriter();
            e.WriteTo(sw, HtmlEncoder.Default, new TemplateContext());

            Assert.Equal("xxx", sw.ToString());
        }

        [Fact]
        public void ShouldLoopArrays()
        {
            var e = new ForStatement(
                new[] { new TextStatement("x") },
                "i",
                new MemberExpression(
                    new IdentifierSegment("items")
                )
            );

            var sw = new StringWriter();
            var context = new TemplateContext();
            context.SetValue("items", new[] { 1, 2, 3 });
            e.WriteTo(sw, HtmlEncoder.Default, context);

            Assert.Equal("xxx", sw.ToString());
        }

        [Fact]
        public void ShouldHandleBreak()
        {
            var e = new ForStatement(
                new Statement[] {
                    new TextStatement("x"),
                    new BreakStatement(),
                    new TextStatement("y")
                },
                "i",
                new MemberExpression(
                    new IdentifierSegment("items")
                )
            );

            var sw = new StringWriter();
            var context = new TemplateContext();
            context.SetValue("items", new[] { 1, 2, 3 });
            e.WriteTo(sw, HtmlEncoder.Default, context);

            Assert.Equal("x", sw.ToString());
        }

        [Fact]
        public void ShouldHandleContinue()
        {
            var e = new ForStatement(
                new Statement[] {
                    new TextStatement("x"),
                    new ContinueStatement(),
                    new TextStatement("y")
                },
                "i",
                new MemberExpression(
                    new IdentifierSegment("items")
                )
            );

            var sw = new StringWriter();
            var context = new TemplateContext();
            context.SetValue("items", new[] { 1, 2, 3 });
            e.WriteTo(sw, HtmlEncoder.Default, context);

            Assert.Equal("xxx", sw.ToString());
        }

        [Fact]
        public void ForShouldProvideHelperVariables()
        {
            Statement CreateMemberStatement(string p)
            {
                return new OutputStatement(new MemberExpression(new IdentifierSegment("forloop"), new IdentifierSegment(p)), null);
            }

            var e = new ForStatement(
                new Statement[] {
                    CreateMemberStatement("length"),
                    CreateMemberStatement("index"),
                    CreateMemberStatement("index0"),
                    CreateMemberStatement("rindex"),
                    CreateMemberStatement("rindex0"),
                    CreateMemberStatement("first"),
                    CreateMemberStatement("last")
                },
                "i",
                new MemberExpression(
                    new IdentifierSegment("items")
                )
            );

            var sw = new StringWriter();
            var context = new TemplateContext();
            context.SetValue("items", new[] { 1, 2, 3 });
            e.WriteTo(sw, HtmlEncoder.Default, context);

            Assert.Equal("31023truefalse32112falsefalse33201falsetrue", sw.ToString());
        }
    }
}
