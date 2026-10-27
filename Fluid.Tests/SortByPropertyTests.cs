using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Fluid.Tests;

public class SortByPropertyTests
{
#if COMPILED
    private static readonly FluidParser _parser = new FluidParser().Compile();
#else
    private static readonly FluidParser _parser = new FluidParser();
#endif

    private static async Task<string> RenderAsync(string source, params object[] values)
    {
        var items = new List<Dictionary<string, object>>();
        foreach (var value in values)
        {
            items.Add(new Dictionary<string, object> { ["n"] = value });
        }

        var context = new TemplateContext();
        context.SetValue("items", items);

        return await _parser.Parse(source).RenderAsync(context);
    }

    [Fact]
    public async Task SortComparesNumbersAsNumbers()
    {
        const string source = "{% assign sorted = items | sort: 'n' %}{% for i in sorted %}{{ i.n }} {% endfor %}";

        Assert.Equal("9 10 100 ", await RenderAsync(source, 10, 9, 100));
        Assert.Equal("-2 1.5 3 ", await RenderAsync(source, 3, -2, 1.5m));
    }

    [Fact]
    public async Task SortNaturalComparesNumbersAsStrings()
    {
        // Shopify Liquid: sort_natural compares the string form of the values, so this is the expected order.
        const string source = "{% assign sorted = items | sort_natural: 'n' %}{% for i in sorted %}{{ i.n }} {% endfor %}";

        Assert.Equal("10 100 9 ", await RenderAsync(source, 10, 9, 100));
    }

    [Fact]
    public async Task SortNaturalWithoutPropertyComparesNumbersAsStrings()
    {
        // Checked against Shopify Liquid.
        Assert.Equal("10,11,8,9", await _parser.Parse("{{ (8..11) | sort_natural | join: ',' }}").RenderAsync(new TemplateContext()));
    }

    [Theory]
    [InlineData("sort")]
    [InlineData("sort_natural")]
    public async Task MissingValuesStayLast(string filter)
    {
        var source = $"{{% assign sorted = items | {filter}: 'n' %}}{{% for i in sorted %}}[{{{{ i.n }}}}]{{% endfor %}}";

        Assert.Equal("[10][100][9][]", await RenderAsync(source, "10", null, "9", "100"));
    }

    [Fact]
    public async Task StringsAreStillSortedAsStrings()
    {
        Assert.Equal("10 100 9 ", await RenderAsync("{% assign sorted = items | sort: 'n' %}{% for i in sorted %}{{ i.n }} {% endfor %}", "10", "9", "100"));
        Assert.Equal("B a c ", await RenderAsync("{% assign sorted = items | sort: 'n' %}{% for i in sorted %}{{ i.n }} {% endfor %}", "c", "a", "B"));
        Assert.Equal("a B c ", await RenderAsync("{% assign sorted = items | sort_natural: 'n' %}{% for i in sorted %}{{ i.n }} {% endfor %}", "c", "a", "B"));
    }
}
