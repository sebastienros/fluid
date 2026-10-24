using System.Threading.Tasks;
using Xunit;

namespace Fluid.Tests;

public class CycleKeyTests
{
#if COMPILED
    private static readonly FluidParser _parser = new FluidParser().Compile();
#else
    private static readonly FluidParser _parser = new FluidParser();
#endif

    [Theory]
    // Different values, same number of them: separate counters.
    [InlineData("{% cycle 'a', 'b' %}{% cycle 'x', 'y' %}{% cycle 'a', 'b' %}", "axb")]
    [InlineData("{% cycle 1, 2 %}{% cycle 3, 4 %}{% cycle 1, 2 %}", "132")]
    [InlineData("{% cycle '1', '2' %}{% cycle 1, 2 %}{% cycle '1', '2' %}", "112")]
    [InlineData("{% cycle 'a,b' %}{% cycle 'a', 'b' %}{% cycle 'a', 'b' %}", "a,bab")]
    // Same values: one counter.
    [InlineData("{% cycle 'a', 'b' %}{% cycle 'a', 'b' %}{% cycle 'a', 'b' %}", "aba")]
    [InlineData("{% cycle 'a', 'b' %}{% cycle \"a\", \"b\" %}", "ab")]
    // Named cycles and lookups are unchanged.
    [InlineData("{% cycle 'g': 'a', 'b' %}{% cycle 'g': 'x', 'y' %}", "ay")]
    [InlineData("{% cycle v, 'b' %}{% cycle v, 'b' %}", "aa")]
    public async Task UnnamedCyclesShareCountersOnlyWithTheSameValues(string source, string expected)
    {
        var context = new TemplateContext().SetValue("v", "a");

        Assert.Equal(expected, await _parser.Parse(source).RenderAsync(context));
    }
}
