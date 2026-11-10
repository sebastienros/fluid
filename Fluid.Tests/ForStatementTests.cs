using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Fluid.Ast;
using Fluid.Values;
using Xunit;

namespace Fluid.Tests;

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

        Assert.Equal(0, t.GetValue("z").ToNumberValue(new TemplateContext()));

        var sw = new StringWriter();
        await e.WriteToAsync(sw, HtmlEncoder.Default, t);

        Assert.Equal(1, t.GetValue("z").ToNumberValue(new TemplateContext()));
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

        Assert.Equal("31032truefalse32121falsefalse33210falsetrue", sw.ToString());
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
    public async Task NegativeLimitShouldRenderNothing()
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

        Assert.Equal("", sw.ToString());
    }

    static Statement CreateMemberStatement(string p)
    {
        return new OutputStatement(new MemberExpression(p.Split('.').Select(x => new IdentifierSegment(x)).ToArray()));
    }

    [Theory]
    // Like Liquid, a nil offset starts at the first item and a nil limit doesn't limit the items.
    [InlineData("{% for x in (1..3) limit: missing %}{{ x }}{% endfor %}", "123")]
    [InlineData("{% for x in (1..3) offset: missing %}{{ x }}{% endfor %}", "123")]
    [InlineData("{% for x in (1..3) reversed offset: nil limit: nil %}{{ x }}{% endfor %}", "321")]
    [InlineData("{% for x in (1..3) offset: 1 limit: missing %}{{ x }}{% endfor %}", "23")]
    public async Task NilLimitAndOffsetAreIgnored(string source, string expected)
    {
        Assert.Equal(expected, await new FluidParser().Parse(source).RenderAsync(new TemplateContext()));
    }

    [Theory]
    // Like Liquid, a number ends where a word starts, so attributes don't need a space between them.
    [InlineData("{% for x in (1..3) offset:1limit:1 %}{{ x }}{% endfor %}", "2")]
    [InlineData("{% for x in (1..3) limit:1offset:1 %}{{ x }}{% endfor %}", "2")]
    [InlineData("{% for x in (1..3) limit: 2offset: 1 %}{{ x }}{% endfor %}", "23")]
    [InlineData("{% for x in (1..3) limit:2offset:u %}{{ x }}{% endfor %}", "12")]
    [InlineData("{% for x in (1..3) limit: -1 %}{{ x }}{% endfor %}|{% for x in (1..3) limit: x %}{{ x }}{% endfor %}", "|123")]
    // Like Liquid, tablerow reads the glued attributes as a single value.
    [InlineData("{% tablerow x in (1..3) cols:1limit:2 %}{{ x }}{% endtablerow %}", "<tr class=\"row1\">\n<td class=\"col1\">1</td><td class=\"col2\">2</td><td class=\"col3\">3</td></tr>\n")]
    public async Task GluedLoopAttributes(string source, string expected)
    {
        Assert.Equal(expected, await new FluidParser().Parse(source).RenderAsync(new TemplateContext()));
    }

    [Theory]
    // Like Liquid, a string is a single item, or none when it is empty, whatever the limit and offset.
    [InlineData("{% for i in \"c\" limit: 0 %}[{{ i }}]{% else %}E{% endfor %}", "[c]")]
    [InlineData("{% for i in \"c\" offset: 1 %}[{{ i }}]{% endfor %}", "[c]")]
    [InlineData("{% for i in \"c\" offset: \"1\" limit: 0 %}[{{ i }}]{% endfor %}", "[c]")]
    [InlineData("{% for i in \"c\" limit: -1 %}[{{ i }}]{% endfor %}", "[c]")]
    [InlineData("{% for i in \"c\" reversed offset: 5 limit: 0 %}[{{ i }}{{ forloop.length }}{{ forloop.first }}{{ forloop.last }}]{% endfor %}", "[c1truetrue]")]
    [InlineData("{% for i in \"\" limit: 1 %}[{{ i }}]{% else %}E{% endfor %}", "E")]
    [InlineData("{% for i in \"abc\" offset: continue limit: 0 %}[{{ i }}]{% endfor %}{% for i in \"abc\" offset: continue %}[{{ i }}]{% endfor %}", "[abc][abc]")]
    // The continue cursor still moves from the offset, by one item, or none for an empty string.
    [InlineData("{% assign s = \"abc\" %}{% for i in s offset: 2 %}{% endfor %}{% assign s = \"a,b,c,d\" | split: \",\" %}{% for i in s offset: continue %}{{ i }}{% endfor %}", "d")]
    [InlineData("{% assign s = \"\" %}{% for i in s offset: 2 %}{% endfor %}{% assign s = \"a,b,c,d\" | split: \",\" %}{% for i in s offset: continue %}{{ i }}{% endfor %}", "cd")]
    [InlineData("{% assign s = \"abc\" %}{% for i in s offset: -1 %}{% endfor %}{% assign s = \"a,b,c,d\" | split: \",\" %}{% for i in s offset: continue %}{{ i }}{% endfor %}", "abcd")]
    public async Task StringsIgnoreLimitAndOffset(string source, string expected)
    {
        Assert.Equal(expected, await new FluidParser().Parse(source).RenderAsync(new TemplateContext()));
    }

    [Fact]
    public async Task StringsStillEvaluateTheLimit()
    {
        var template = new FluidParser().Parse("{% for i in \"c\" limit: \"x\" %}[{{ i }}]{% endfor %}");

        await Assert.ThrowsAsync<LiquidException>(() => template.RenderAsync(new TemplateContext()).AsTask());
    }

    [Theory]
    // Like Liquid's ForloopDrop, the properties of forloop can be read with an indexer.
    [InlineData("{% for x in (1..3) %}{{ forloop['index'] }}{% assign k = 'last' %}{{ forloop[k] }}{% if forloop[\"first\"] %}F{% endif %},{% endfor %}", "1falseF,2false,3true,")]
    [InlineData("{% for x in (1..2) %}{% for y in (1..2) %}{{ forloop['parentloop']['index'] }}{{ forloop['missing'] }}{% endfor %}{% endfor %}", "1122")]
    public async Task ForloopPropertiesCanBeIndexed(string source, string expected)
    {
        Assert.Equal(expected, await new FluidParser().Parse(source).RenderAsync(new TemplateContext()));
    }

    [Theory]
    // Like Liquid, a string limit or offset is read like Ruby's Integer(): with a 0x, 0b, 0o or 0d prefix, a leading
    // 0 for octal, and single underscores between digits.
    [InlineData("{% for x in (1..20) limit: '0x3' %}{{ x }},{% endfor %}|{% for x in (1..20) offset: '1_7' %}{{ x }},{% endfor %}", "1,2,3,|18,19,20,")]
    [InlineData("{% for x in (1..20) limit: '010' %}{{ x }},{% endfor %}|{% for x in (1..5) offset: '0b1' limit: ' 0o2 ' %}{{ x }},{% endfor %}", "1,2,3,4,5,6,7,8,|2,3,")]
    public async Task StringLimitAndOffsetAreReadLikeRubyIntegers(string source, string expected)
    {
        Assert.Equal(expected, await new FluidParser().Parse(source).RenderAsync(new TemplateContext()));
    }

    [Theory]
    [InlineData("{% for x in (1..5) limit: '3.5' %}{% endfor %}", "for: limit is not a number")]
    [InlineData("{% for x in (1..5) offset: '08' %}{% endfor %}", "for: offset is not a number")]
    [InlineData("{% for x in (1..5) offset: '1__0' %}{% endfor %}", "for: offset is not a number")]
    [InlineData("{% for x in (1..5) offset: '' %}{% endfor %}", "for: offset is not a number")]
    [InlineData("{% for x in (1..5) limit: '0_3' %}{% endfor %}", "for: limit is not a number")]
    public async Task InvalidStringLimitAndOffsetThrow(string source, string message)
    {
        var exception = await Assert.ThrowsAsync<LiquidException>(() => new FluidParser().Parse(source).RenderAsync(new TemplateContext()).AsTask());

        Assert.Equal(message, exception.Message);
    }
}
