using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Fluid.Tests;

public class DictionaryContainsTests
{
#if COMPILED
    private static readonly FluidParser _parser = new FluidParser().Compile();
#else
    private static readonly FluidParser _parser = new FluidParser();
#endif

    private static async Task<string> RenderAsync(string condition, object dictionary)
    {
        var context = new TemplateContext();
        context.SetValue("d", dictionary);
        context.SetValue("key", "a");

        return await _parser.Parse($"{{% if {condition} %}}yes{{% else %}}no{{% endif %}}").RenderAsync(context);
    }

    [Theory]
    [InlineData("d contains 'a'", "yes")]
    [InlineData("d contains key", "yes")]
    [InlineData("d contains 'b'", "no")]
    // Keys are compared exactly, and values are not keys.
    [InlineData("d contains 'A'", "no")]
    [InlineData("d contains 1", "no")]
    [InlineData("d contains 'x'", "no")]
    [InlineData("d contains nil", "no")]
    [InlineData("d contains missing", "no")]
    public async Task ContainsChecksKeys(string condition, string expected)
    {
        var dictionary = new Dictionary<string, object> { ["a"] = 1, ["n"] = null, ["z"] = "x" };

        Assert.Equal(expected, await RenderAsync(condition, dictionary));
    }

    [Fact]
    public async Task KeyWithANilValueIsContained()
    {
        Assert.Equal("yes", await RenderAsync("d contains 'n'", new Dictionary<string, object> { ["n"] = null }));
    }

    [Fact]
    public async Task EmptyDictionaryContainsNothing()
    {
        Assert.Equal("no", await RenderAsync("d contains 'a'", new Dictionary<string, object>()));
    }

    [Theory]
    [InlineData("d contains 5", "yes")]
    [InlineData("d contains '5'", "yes")]
    [InlineData("d contains 6", "no")]
    public async Task NonStringKeysAreMatchedLikeIndexing(string condition, string expected)
    {
        Assert.Equal(expected, await RenderAsync(condition, new Dictionary<int, string> { [5] = "five" }));
    }
}
