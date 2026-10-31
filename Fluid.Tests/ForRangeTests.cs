using System.Threading.Tasks;
using Xunit;

namespace Fluid.Tests;

public class ForRangeTests
{
    private static readonly FluidParser _parser = new FluidParser();

    public sealed class Bounds
    {
        public int Reads { get; private set; }

        public int Last
        {
            get
            {
                Reads++;
                return 3;
            }
        }
    }

    [Theory]
    [InlineData("{{ (1..5) }}|{{ (5..1) }}", "1..5|5..1")]
    [InlineData("{{ (1..5) | join: '#' }}|{{ (1..5) | reverse | join: '#' }}|{{ (1..5) | size }}", "1#2#3#4#5|5#4#3#2#1|5")]
    [InlineData("{{ (1..5) | slice: 1, 3 }}|{{ (1..5) | upcase }}|{{ (1..3) | json }}", "..5|1..5|[1,2,3]")]
    [InlineData("{% assign r = (1..5) %}{{ r.first }}|{{ r.last }}|{{ r.size }}|{{ r[1] }}|{{ r | first }}|{{ r | last }}", "1|5|5||1|5")]
    [InlineData("{% assign r = (5..1) %}{{ r.first }}|{{ r.last }}|{{ r.size }}|{{ r | first }}|{{ r | last }}", "5|1|0|5|1")]
    [InlineData("{% assign r = (1..5) %}{% if r contains 3.5 %}y{% endif %}{% if r contains 6 %}n{% endif %}{% for i in r reversed %}{{ i }}{% endfor %}", "y54321")]
    [InlineData("{% assign a = (1..3) %}{% assign b = (1..3) %}{% if a == b %}eq{% endif %}", "eq")]
    [InlineData("{{ (2.7..4) | join }}|{{ (s..4) | join }}|{{ (t..2) | join }}|{{ (nothing..1) | join }}", "2 3 4|2 3 4|0 1 2|0 1")]
    public async Task RangesBehaveLikeLiquidRanges(string source, string expected)
    {
        var template = _parser.Parse(source);
        var context = new TemplateContext().SetValue("s", " 2.7").SetValue("t", "foo");

        Assert.Equal(expected, await template.RenderAsync(context));
    }

    [Theory]
    [InlineData("{{ (x..5) | join }}")]
    [InlineData("{% for i in (1..x) %}{{ i }}{% endfor %}")]
    [InlineData("{{ (b..5) | join }}")]
    public async Task NonIntegerRangeBoundsFromVariablesAreErrors(string source)
    {
        var template = _parser.Parse(source);
        var context = new TemplateContext().SetValue("x", 2.3m).SetValue("b", true);

        await Assert.ThrowsAsync<LiquidException>(() => template.RenderAsync(context).AsTask());
    }

    [Theory]
    // Like Liquid, the name is the range as written, not its evaluated bounds.
    [InlineData("{% for i in (1..3) %}{{ forloop.name }} {% endfor %}", "i-(1..3) i-(1..3) i-(1..3) ")]
    [InlineData("{% for i in (first..last) %}{{ forloop.name }} {% endfor %}", "i-(first..last) i-(first..last) ")]
    [InlineData("{% for i in (first..3) %}{{ forloop.name }} {% endfor %}", "i-(first..3) i-(first..3) ")]
    [InlineData("{% for i in (-1..0) %}{{ forloop.name }} {% endfor %}", "i-(-1..0) i-(-1..0) ")]
    // Literal bounds keep their text, even when the range truncates them or converts a string.
    [InlineData("{% for i in (1.5..3) %}{{ forloop.name }} {% endfor %}", "i-(1.5..3) i-(1.5..3) i-(1.5..3) ")]
    [InlineData("{% for i in (0..1.0) %}{{ forloop.name }} {% endfor %}", "i-(0..1.0) i-(0..1.0) ")]
    [InlineData("{% for i in ('1'..2) %}{{ forloop.name }} {% endfor %}{% for i in (1..\"1\") %}{{ forloop.name }} {% endfor %}", "i-('1'..2) i-('1'..2) i-(1..\"1\") ")]
    public async Task ForLoopNameDescribesTheRange(string source, string expected)
    {
        var template = _parser.Parse(source);
        var context = new TemplateContext().SetValue("first", 2).SetValue("last", 3);

        Assert.Equal(expected, await template.RenderAsync(context));
    }

    [Theory]
    [InlineData("{% for i in (1..6) limit: 2 %}{{ i }}{% endfor %}|{% for i in (1..6) offset: continue %}{{ i }}{% endfor %}", "12|3456")]
    [InlineData("{% for i in (1..last) limit: 2 %}{{ i }}{% endfor %}|{% for i in (1..last) offset: continue %}{{ i }}{% endfor %}", "12|3456")]
    // Like Liquid, loops are named after the range as written: the same bounds written differently are different loops.
    [InlineData("{% for i in (1..6) limit: 2 %}{{ i }}{% endfor %}|{% for i in (1..last) offset: continue %}{{ i }}{% endfor %}", "12|123456")]
    // A different loop variable is a different loop.
    [InlineData("{% for i in (1..6) limit: 2 %}{{ i }}{% endfor %}|{% for j in (1..6) offset: continue %}{{ j }}{% endfor %}", "12|123456")]
    [InlineData("{% for i in (1.5..3) limit: 1 %}{{ i }}{% endfor %}|{% for i in (1..3) offset: continue %}{{ i }}{% endfor %}", "1|123")]
    // Like Liquid, a loop moves the cursor when it starts: a nested loop over the same range continues after it.
    [InlineData("{% for i in (1..6) limit: 2 %}{% for i in (1..6) offset: continue limit: 1 %}[{{ i }}]{% endfor %}{{ i }}{% endfor %}|{% for i in (1..6) offset: continue %}{{ i }}{% endfor %}", "[3]1[4]2|56")]
    [InlineData("{% for i in (1..6) limit: 1 %}{% break %}{% endfor %}{% for i in (1..6) offset: continue limit: 2 %}{{ i }}{% endfor %}", "23")]
    public async Task OffsetContinueFollowsTheRange(string source, string expected)
    {
        var template = _parser.Parse(source);
        var context = new TemplateContext().SetValue("last", 6);

        Assert.Equal(expected, await template.RenderAsync(context));
    }

    [Fact]
    public async Task RangeBoundsAreEvaluatedOncePerLoop()
    {
        var bounds = new Bounds();
        var template = _parser.Parse("{% for i in (1..bounds.Last) %}{{ i }}{% endfor %}");
        var context = new TemplateContext().SetValue("bounds", bounds);

        Assert.Equal("123", await template.RenderAsync(context));
        Assert.Equal(1, bounds.Reads);
    }

    [Fact]
    public async Task EmptyRangeRendersElse()
    {
        var template = _parser.Parse("{% for i in (first..1) %}{{ i }}{% else %}none{% endfor %}");
        var context = new TemplateContext().SetValue("first", 2);

        Assert.Equal("none", await template.RenderAsync(context));
    }
}
