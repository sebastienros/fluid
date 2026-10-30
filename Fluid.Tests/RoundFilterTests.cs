using System.Threading.Tasks;
using Xunit;

namespace Fluid.Tests;

public class RoundFilterTests
{
#if COMPILED
    private static readonly FluidParser _parser = new FluidParser().Compile();
#else
    private static readonly FluidParser _parser = new FluidParser();
#endif

    [Theory]
    // Halves round away from zero.
    [InlineData("{{ 0.5 | round }}", "1")]
    [InlineData("{{ 2.5 | round }}", "3")]
    [InlineData("{{ 3.5 | round }}", "4")]
    [InlineData("{{ -2.5 | round }}", "-3")]
    [InlineData("{{ 1.125 | round: 2 }}", "1.13")]
    [InlineData("{{ 1.135 | round: 2 }}", "1.14")]
    [InlineData("{{ -1.125 | round: 2 }}", "-1.13")]
    [InlineData("{{ 25 | round: -1 }}", "30")]
    [InlineData("{{ 15 | round: -1 }}", "20")]
    [InlineData("{{ -25 | round: -1 }}", "-30")]
    // Values that aren't halves are unchanged.
    [InlineData("{{ 2.4 | round }}", "2")]
    [InlineData("{{ 2.6 | round }}", "3")]
    [InlineData("{{ 5.666 | round: 1 }}", "5.7")]
    [InlineData("{{ 24 | round: -1 }}", "20")]
    public async Task RoundsHalvesAwayFromZero(string source, string expected)
    {
        Assert.Equal(expected, await _parser.Parse(source).RenderAsync(new TemplateContext()));
    }

    [Theory]
    // A decimal can't have more than 28 digits after the point, or be 10^29 or more.
    [InlineData("{{ 2 | round: 30 }}", "2")]
    [InlineData("{{ 2.5 | round: \"29\" }}", "2.5")]
    [InlineData("{{ 1.5 | round: 2147483648 }}", "1.5")]
    [InlineData("{{ 25 | round: -30 }}", "0")]
    [InlineData("{{ 25 | round: -2147483649 }}", "0")]
    public async Task RoundsBeyondDecimalPrecision(string source, string expected)
    {
        Assert.Equal(expected, await _parser.Parse(source).RenderAsync(new TemplateContext()));
    }
}
