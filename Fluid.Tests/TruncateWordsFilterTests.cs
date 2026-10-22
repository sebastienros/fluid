using System.Threading.Tasks;
using Xunit;

namespace Fluid.Tests;

public class TruncateWordsFilterTests
{
#if COMPILED
    private static readonly FluidParser _parser = new FluidParser().Compile();
#else
    private static readonly FluidParser _parser = new FluidParser();
#endif

    [Theory]
    // As many words as the limit: nothing is dropped, so no ellipsis.
    [InlineData("{{ 'one two three' | truncatewords: 3 }}", "one two three")]
    [InlineData("{{ 'one two three ' | truncatewords: 3 }}", "one two three ")]
    [InlineData("{{ 'one' | truncatewords: 1 }}", "one")]
    [InlineData("{{ 'one' | truncatewords: 0 }}", "one")]
    // Not truncated: the input is returned as it is, whitespace included.
    [InlineData("[{{ '  one   two ' | truncatewords: 5 }}]", "[  one   two ]")]
    // More words than the limit.
    [InlineData("{{ 'one two three four' | truncatewords: 3 }}", "one two three...")]
    [InlineData("{{ 'one  two   three four' | truncatewords: 3, '!' }}", "one two three!")]
    [InlineData("{{ 'one two' | truncatewords: 0 }}", "one...")]
    [InlineData("[{{ '' | truncatewords: 3 }}]", "[]")]
    public async Task AddsTheEllipsisOnlyWhenWordsAreDropped(string source, string expected)
    {
        Assert.Equal(expected, await _parser.Parse(source).RenderAsync(new TemplateContext()));
    }
}
