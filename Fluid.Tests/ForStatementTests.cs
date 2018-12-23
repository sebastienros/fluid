﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Fluid.Ast;
using Fluid.Values;
using Xunit;

namespace Fluid.Tests
{
    public class ForStatementTests
    {
        [Fact]
        public async Task ShouldLoopRange()
        {
            var e = new ForStatement(
                new List<Statement> { new TextStatement("x") },
                "i",
                new RangeExpression(
                    new LiteralExpression(new NumberValue(1)),
                    new LiteralExpression(new NumberValue(3))
                ),
                null, null, false
            );

            var sw = new StringWriter();
            await e.WriteToAsync(sw, HtmlEncoder.Default, new TemplateContext());

            Assert.Equal("xxx", sw.ToString());
        }

        [Fact]
        public async Task ShouldUseCurrentContext()
        {
            var e = new ForStatement(
                new List<Statement> {
                    new AssignStatement("z", new LiteralExpression(new NumberValue(1)))
                    },
                "i",
                new RangeExpression(
                    new LiteralExpression(new NumberValue(1)),
                    new LiteralExpression(new NumberValue(3))
                ),
                null, null, false
            );

            var t = new TemplateContext();
            t.SetValue("z", 0);

            Assert.Equal(0, t.GetValue("z").ToNumberValue());

            var sw = new StringWriter();
            await e.WriteToAsync(sw, HtmlEncoder.Default, t);

            Assert.Equal(1, t.GetValue("z").ToNumberValue());
        }

        [Fact]
        public async Task ShouldLoopArrays()
        {
            var e = new ForStatement(
                new List<Statement> { new TextStatement("x") },
                "i",
                new MemberExpression(
                    new IdentifierSegment("items")
                ),
                null, null, false
            );

            var sw = new StringWriter();
            var context = new TemplateContext();
            context.SetValue("items", new[] { 1, 2, 3 });
            await e.WriteToAsync(sw, HtmlEncoder.Default, context);

            Assert.Equal("xxx", sw.ToString());
        }

        [Fact]
        public async Task ShouldHandleBreak()
        {
            var e = new ForStatement(
                new List<Statement> {
                    new TextStatement("x"),
                    new BreakStatement(),
                    new TextStatement("y")
                },
                "i",
                new MemberExpression(
                    new IdentifierSegment("items")
                ),
                null, null, false
            );

            var sw = new StringWriter();
            var context = new TemplateContext();
            context.SetValue("items", new[] { 1, 2, 3 });
            await e.WriteToAsync(sw, HtmlEncoder.Default, context);

            Assert.Equal("x", sw.ToString());
        }

        [Fact]
        public async Task ShouldHandleContinue()
        {
            var e = new ForStatement(
                new List<Statement> {
                    new TextStatement("x"),
                    new ContinueStatement(),
                    new TextStatement("y")
                },
                "i",
                new MemberExpression(
                    new IdentifierSegment("items")
                ),
                null, null, false
            );

            var sw = new StringWriter();
            var context = new TemplateContext();
            context.SetValue("items", new[] { 1, 2, 3 });
            await e.WriteToAsync(sw, HtmlEncoder.Default, context);

            Assert.Equal("xxx", sw.ToString());
        }

        [Fact]
        public async Task ForShouldProvideHelperVariables()
        {

            var e = new ForStatement(
                new List<Statement> {
                    CreateMemberStatement("forloop.length"),
                    CreateMemberStatement("forloop.index"),
                    CreateMemberStatement("forloop.index0"),
                    CreateMemberStatement("forloop.rindex"),
                    CreateMemberStatement("forloop.rindex0"),
                    CreateMemberStatement("forloop.first"),
                    CreateMemberStatement("forloop.last")
                },
                "i",
                new MemberExpression(
                    new IdentifierSegment("items")
                ),
                null, null, false
            );

            var sw = new StringWriter();
            var context = new TemplateContext();
            context.SetValue("items", new[] { 1, 2, 3 });
            await e.WriteToAsync(sw, HtmlEncoder.Default, context);

            Assert.Equal("31023truefalse32112falsefalse33201falsetrue", sw.ToString());
        }

        [Fact]
        public async Task ForEvaluatesOptions()
        {
            var e = new ForStatement(
                new List<Statement> { CreateMemberStatement("i") },
                "i",
                new RangeExpression(
                    new LiteralExpression(new NumberValue(1)),
                    new LiteralExpression(new NumberValue(5))
                ),
                new LiteralExpression(new NumberValue(3)),
                new LiteralExpression(new NumberValue(2)),
                true
            );

            var sw = new StringWriter();
            await e.WriteToAsync(sw, HtmlEncoder.Default, new TemplateContext());

            Assert.Equal("543", sw.ToString());
        }


        Statement CreateMemberStatement(string p)
        {
            return new OutputStatement(new MemberExpression(p.Split('.').Select(x => new IdentifierSegment(x)).ToArray()));
        }
    }
}
