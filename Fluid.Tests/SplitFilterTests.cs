using System.Threading.Tasks;
using Xunit;

namespace Fluid.Tests;

public class SplitFilterTests
{
#if COMPILED
    private static readonly FluidParser _parser = new FluidParser().Compile();
#else
    private static readonly FluidParser _parser = new FluidParser();
#endif

    [Theory]
    // Trailing empty strings are dropped.
    [InlineData("{{ 'a,b,' | split: ',' | size }}", "2")]
    [InlineData("{{ 'a,b,' | split: ',' | last }}", "b")]
    [InlineData("{{ 'a,b,,,' | split: ',' | join: '|' }}", "a|b")]
    [InlineData("{{ 'a, b, ' | split: ', ' | join: '|' }}", "a|b")]
    [InlineData("{{ ',,' | split: ',' | size }}", "0")]
    [InlineData("{{ ',' | split: ',' | size }}", "0")]
    // Leading and inner empty strings are kept.
    [InlineData("{{ ',a' | split: ',' | join: '|' }}", "|a")]
    [InlineData("{{ 'a,,b' | split: ',' | join: '|' }}", "a||b")]
    [InlineData("{{ 'a,b' | split: ',' | join: '|' }}", "a|b")]
    public async Task DropsTrailingEmptyStrings(string source, string expected)
    {
        Assert.Equal(expected, await _parser.Parse(source).RenderAsync(new TemplateContext()));
    }
}
