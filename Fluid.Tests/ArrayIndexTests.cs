using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Fluid.Tests;

public class ArrayIndexTests
{
#if COMPILED
    private static readonly FluidParser _parser = new FluidParser().Compile();
#else
    private static readonly FluidParser _parser = new FluidParser();
#endif

    // Checked against Shopify Liquid: only an integer names an item.
    [Theory]
    [InlineData("{{ a[1] }}", "y")]
    [InlineData("{{ a[-1] }}", "y")]
    [InlineData("{% assign i = '1' | plus: 0 %}{{ a[i] }}", "y")]
    [InlineData("[{{ a['foo'] }}]", "[]")]
    [InlineData("[{{ a['1'] }}]", "[]")]
    [InlineData("[{{ a[1.7] }}]", "[]")]
    [InlineData("[{{ a[1.0] }}]", "[]")]
    [InlineData("[{{ a[true] }}]", "[]")]
    [InlineData("[{{ a[nil] }}]", "[]")]
    public async Task OnlyIntegersIndexArrays(string source, string expected)
    {
        var template = _parser.Parse(source);

        // An array, and an enumerable that isn't a list.
        Assert.Equal(expected, await template.RenderAsync(new TemplateContext().SetValue("a", new[] { "x", "y" })));
        Assert.Equal(expected, await template.RenderAsync(new TemplateContext().SetValue("a", new Queue<string>(["x", "y"]))));
    }
}
