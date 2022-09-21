using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Fluid.Ast;
using Fluid.Values;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Fluid.Tests
{
    public class ForStatementTests
    {
        [Fact]
        public async Task ZeroBasedLoopRangeShouldNotHasExtraStep()
        {
            var e = new ForStatement(
                new List<Statement> { new TextSpanStatement("x") },
                "i",
                new RangeExpression(
                    new LiteralExpression(NumberValue.Create(0)),
                    new LiteralExpression(NumberValue.Create(1))
                ),
                null, null, false
            );

            var sw = new StringWriter();
            await e.WriteToAsync(sw, HtmlEncoder.Default, new TemplateContext());

            Assert.Equal("xx", sw.ToString());
        }

        [Fact]
        public async Task ShouldLoopRange()
        {
            var e = new ForStatement(
                new List<Statement> { new TextSpanStatement("x") },
                "i",
                new RangeExpression(
                    new LiteralExpression(NumberValue.Create(1)),
                    new LiteralExpression(NumberValue.Create(3))
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
                    new AssignStatement("z", new LiteralExpression(NumberValue.Create(1)))
                    },
                "i",
                new RangeExpression(
                    new LiteralExpression(NumberValue.Create(1)),
                    new LiteralExpression(NumberValue.Create(3))
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
                new List<Statement> { new TextSpanStatement("x") },
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
                    new TextSpanStatement("x"),
                    new BreakStatement(),
                    new TextSpanStatement("y")
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
                    new TextSpanStatement("x"),
                    new ContinueStatement(),
                    new TextSpanStatement("y")
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
        public async Task ForShouldBeNestable()
        {
            /*
             * {% for i in items %}
             *   {{ forloop.index }}
             *   {% for j in items %}
             *     {{ forloop.index }}
             *   {% endfor %}
             * {% endfor %}
             * 
             */

            var nested = new ForStatement(
                new List<Statement> {
                    CreateMemberStatement("forloop.index")
                },
                "j",
                new MemberExpression(
                    new IdentifierSegment("items")
                ),
                null, null, false
            );

            var outer = new ForStatement(
                new List<Statement> {
                    CreateMemberStatement("forloop.index"),
                    nested
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
            await outer.WriteToAsync(sw, HtmlEncoder.Default, context);

            Assert.Equal("112321233123", sw.ToString());
        }

        [Fact]
        public async Task NestedForShouldProvideParentLoop()
        {
            /*
             * {% for i in items %}
             *   {{ forloop.index }}
             *   {% for j in items %}
             *     {{ parentloop.index }}
             *   {% endfor %}
             * {% endfor %}
             * 
             */

            var nested = new ForStatement(
                new List<Statement> {
                    CreateMemberStatement("parentloop.index")
                },
                "j",
                new MemberExpression(
                    new IdentifierSegment("items")
                ),
                null, null, false
            );

            var outer = new ForStatement(
                new List<Statement> {
                    CreateMemberStatement("forloop.index"),
                    nested
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
            await outer.WriteToAsync(sw, HtmlEncoder.Default, context);

            Assert.Equal("111122223333", sw.ToString());
        }

        [Fact]
        public async Task ForEvaluatesOptions()
        {
            var e = new ForStatement(
                new List<Statement> { CreateMemberStatement("i") },
                "i",
                new RangeExpression(
                    new LiteralExpression(NumberValue.Create(1)),
                    new LiteralExpression(NumberValue.Create(5))
                ),
                new LiteralExpression(NumberValue.Create(3)),
                new LiteralExpression(NumberValue.Create(2)),
                true
            );

            var sw = new StringWriter();
            await e.WriteToAsync(sw, HtmlEncoder.Default, new TemplateContext());

            Assert.Equal("543", sw.ToString());
        }

        [Fact]
        public async Task ShouldExecuteElseOnEmptyArray()
        {
            var e = new ForStatement(
                new List<Statement> { new TextSpanStatement("x") },
                "i",
                new MemberExpression(
                    new IdentifierSegment("items")
                ),
                null, null, false,
                new ElseStatement(new List<Statement> { new TextSpanStatement("y") })
            );

            var sw = new StringWriter();
            var context = new TemplateContext();
            context.SetValue("items", Array.Empty<int>());
            await e.WriteToAsync(sw, HtmlEncoder.Default, context);

            Assert.Equal("y", sw.ToString());
        }

        [Fact]
        public async Task ShouldNotExecuteElseOnNonEmptyArray()
        {
            var e = new ForStatement(
                new List<Statement> { new TextSpanStatement("x") },
                "i",
                new MemberExpression(
                    new IdentifierSegment("items")
                ),
                null, null, false,
                new ElseStatement(new List<Statement> { new TextSpanStatement("y") })
            );

            var sw = new StringWriter();
            var context = new TemplateContext();
            context.SetValue("items", new int[] { 1, 2, 3 });
            await e.WriteToAsync(sw, HtmlEncoder.Default, context);

            Assert.Equal("xxx", sw.ToString());
        }
            
        [Fact]
        public async Task ForEvaluatesMemberOptions()
        {
            var context = new TemplateContext()
                .SetValue("limit", 3)
                .SetValue("offset", 2)
                ;

            var e = new ForStatement(
                new List<Statement> { CreateMemberStatement("i") },
                "i",
                new RangeExpression(
                    new LiteralExpression(NumberValue.Create(1)),
                    new LiteralExpression(NumberValue.Create(5))
                ),
                new MemberExpression(new IdentifierSegment("limit")),
                new MemberExpression(new IdentifierSegment("offset")),
                true
            );

            var sw = new StringWriter();
            await e.WriteToAsync(sw, HtmlEncoder.Default, context);

            Assert.Equal("543", sw.ToString());
        }

        [Fact]
        public async Task ForEvaluatesMemberOptionsLimitOnly()
        {
            var context = new TemplateContext()
                .SetValue("limit", 3)
                ;

            var e = new ForStatement(
                new List<Statement> { CreateMemberStatement("i") },
                "i",
                new RangeExpression(
                    new LiteralExpression(NumberValue.Create(1)),
                    new LiteralExpression(NumberValue.Create(5))
                ),
                new MemberExpression(new IdentifierSegment("limit")),
                null,
                true
            );

            var sw = new StringWriter();
            await e.WriteToAsync(sw, HtmlEncoder.Default, context);

            Assert.Equal("321", sw.ToString());
        }

        [Fact]
        public async Task ForEvaluatesMemberOptionsOffsetOnly()
        {
            var context = new TemplateContext()
                .SetValue("offset", 3)
                ;

            var e = new ForStatement(
                new List<Statement> { CreateMemberStatement("i") },
                "i",
                new RangeExpression(
                    new LiteralExpression(NumberValue.Create(1)),
                    new LiteralExpression(NumberValue.Create(5))
                ),
                null,
                new MemberExpression(new IdentifierSegment("offset")),
                true
            );

            var sw = new StringWriter();
            await e.WriteToAsync(sw, HtmlEncoder.Default, context);

            Assert.Equal("54", sw.ToString());
        }

        [Fact]
        public async Task NegativeTargetShouldNotRenderLoop()
        {
            var e = new ForStatement(
                new List<Statement> { new TextSpanStatement("x") },
                "i",
                new RangeExpression(
                    new LiteralExpression(NumberValue.Create(0)),
                    new LiteralExpression(NumberValue.Create(-1))
                ),
                null, null, false
            );

            var sw = new StringWriter();
            await e.WriteToAsync(sw, HtmlEncoder.Default, new TemplateContext());

            Assert.Equal("", sw.ToString());
        }
        
        [Fact]
        public async Task InvalidRangeShouldNotRenderLoop()
        {
            var e = new ForStatement(
                new List<Statement> { new TextSpanStatement("x") },
                "i",
                new RangeExpression(
                    new LiteralExpression(NumberValue.Create(-10)),
                    new LiteralExpression(NumberValue.Create(-20))
                ),
                null, null, false
            );

            var sw = new StringWriter();
            await e.WriteToAsync(sw, HtmlEncoder.Default, new TemplateContext());

            Assert.Equal("", sw.ToString());
        }
        
        [Fact]
        public async Task NegativeLimitShouldStripFromEnd()
        {
            var context = new TemplateContext()
                    .SetValue("limit", -3)
                ;

            var e = new ForStatement(
                new List<Statement> { CreateMemberStatement("i") },
                "i",
                new RangeExpression(
                    new LiteralExpression(NumberValue.Create(1)),
                    new LiteralExpression(NumberValue.Create(9))
                ),
                new MemberExpression(new IdentifierSegment("limit")),
                offset: null,
                false
            );

            var sw = new StringWriter();
            await e.WriteToAsync(sw, HtmlEncoder.Default, context);

            Assert.Equal("123456", sw.ToString());
        }

        static Statement CreateMemberStatement(string p)
        {
            return new OutputStatement(new MemberExpression(p.Split('.').Select(x => new IdentifierSegment(x)).ToArray()));
        }
    }
}
