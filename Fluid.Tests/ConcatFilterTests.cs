using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Fluid.Tests;

public class ConcatFilterTests
{
#if COMPILED
    private static readonly FluidParser _parser = new FluidParser().Compile();
#else
    private static readonly FluidParser _parser = new FluidParser();
#endif

    private static async Task<string> RenderAsync(string source, object items, object more)
    {
        var context = new TemplateContext();
        context.SetValue("items", items);
        context.SetValue("more", more);

        return await _parser.Parse(source).RenderAsync(context);
    }

    [Fact]
    public async Task DictionariesAreKeptOnce()
    {
        var items = new[] { new Dictionary<string, object> { ["a"] = 1, ["b"] = 2, ["c"] = 3 } };
        var more = new[] { new Dictionary<string, object> { ["a"] = 4 } };

        var result = await RenderAsync("{% assign all = items | concat: more %}{{ all | size }}:{% for d in all %}{{ d.a }} {% endfor %}", items, more);

        Assert.Equal("2:1 4 ", result);
    }

    [Fact]
    public async Task EmptyDictionariesAreKept()
    {
        var items = new[] { new Dictionary<string, object>() };
        var more = new[] { new Dictionary<string, object>() };

        Assert.Equal("2", await RenderAsync("{{ items | concat: more | size }}", items, more));
    }

    [Fact]
    public async Task NilItemsAreKept()
    {
        var items = new object[] { 1, null, 2 };
        var more = new object[] { null, 3 };

        Assert.Equal("1,,2,,3|5", await RenderAsync("{% assign all = items | concat: more %}{{ all | join: ',' }}|{{ all | size }}", items, more));
    }

    [Fact]
    public async Task NestedArraysAreStillFlattened()
    {
        var items = new object[] { 1, new object[] { 2, new object[] { 3 } } };
        var more = new object[] { 4 };

        Assert.Equal("1,2,3,4", await RenderAsync("{{ items | concat: more | join: ',' }}", items, more));
    }

    [Fact]
    public async Task OtherItemsAreUnchanged()
    {
        var items = new object[] { "a", 1, true };
        var more = new object[] { "b" };

        Assert.Equal("a,1,true,b", await RenderAsync("{{ items | concat: more | join: ',' }}", items, more));
    }
}
