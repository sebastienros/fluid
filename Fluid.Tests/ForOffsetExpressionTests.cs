using System.Threading.Tasks;
using Xunit;

namespace Fluid.Tests;

public class ForOffsetExpressionTests
{
#if COMPILED
    private static readonly FluidParser _parser = new FluidParser().Compile();
#else
    private static readonly FluidParser _parser = new FluidParser();
#endif

    private static Task<string> RenderAsync(string source)
    {
        var context = new TemplateContext()
            .SetValue("items", new[] { 1, 2, 3 })
            .SetValue("x", 1)
            .SetValue("continue", 2);

        return _parser.Parse(source).RenderAsync(context).AsTask();
    }

    [Theory]
    // An offset that is a single bracketed name parses and is evaluated.
    [InlineData("{% for i in items offset: ['x'] %}{{ i }}{% endfor %}", "23")]
    [InlineData("{% for i in items offset: ['x'] limit: 1 %}{{ i }}{% endfor %}", "2")]
    // A variable named continue in brackets is a variable, not the continue keyword.
    [InlineData("{% for i in items offset: ['continue'] %}{{ i }}{% endfor %}", "3")]
    // Unchanged.
    [InlineData("{% for i in items offset: x %}{{ i }}{% endfor %}", "23")]
    [InlineData("{% for i in items limit: 1 %}{{ i }}{% endfor %}{% for i in items offset: continue %}{{ i }}{% endfor %}", "123")]
    public async Task OffsetExpressions(string source, string expected)
    {
        Assert.Equal(expected, await RenderAsync(source));
    }

    [Theory]
    // Checked against Shopify Liquid: an offset past the end moves the continue cursor there.
    [InlineData("{% for i in items limit: 1 %}{{ i }}{% endfor %}|{% for i in items offset: 5 %}{{ i }}{% else %}E{% endfor %}|{% for i in items offset: continue %}{{ i }}{% endfor %}", "1|E|")]
    [InlineData("{% for i in items limit: 1 %}{{ i }}{% endfor %}|{% for i in items offset: 3 limit: 2 %}{{ i }}{% else %}E{% endfor %}|{% for i in items offset: continue %}{{ i }}{% endfor %}", "1|E|")]
    // A limit of zero or less leaves it where it was.
    [InlineData("{% for i in items limit: 1 %}{{ i }}{% endfor %}|{% for i in items offset: 2 limit: 0 %}{{ i }}{% else %}E{% endfor %}|{% for i in items offset: continue %}{{ i }}{% endfor %}", "1|E|23")]
    [InlineData("{% for i in items limit: 1 %}{{ i }}{% endfor %}|{% for i in items limit: -1 %}{{ i }}{% else %}E{% endfor %}|{% for i in items offset: continue %}{{ i }}{% endfor %}", "1|E|23")]
    public async Task EmptySliceUpdatesContinueCursor(string source, string expected)
    {
        Assert.Equal(expected, await RenderAsync(source));
    }
}
