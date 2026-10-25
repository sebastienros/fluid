using System.Threading.Tasks;
using Xunit;

namespace Fluid.Tests;

public class ModuloFilterTests
{
#if COMPILED
    private static readonly FluidParser _parser = new FluidParser().Compile();
#else
    private static readonly FluidParser _parser = new FluidParser();
#endif

    [Theory]
    // The result has the sign of the divisor.
    [InlineData("{{ -7 | modulo: 3 }}", "2")]
    [InlineData("{{ 7 | modulo: -3 }}", "-2")]
    [InlineData("{{ '-7' | modulo: 3 }}", "2")]
    [InlineData("{{ -7.5 | modulo: 2 }}", "0.5")]
    [InlineData("{{ 7.5 | modulo: -2 }}", "-0.5")]
    [InlineData("{{ -10.1 | modulo: 7.0 }}", "3.9")]
    [InlineData("{{ -1 | modulo: 2.0 }}", "1.0")]
    // Unchanged when the signs match or nothing is left.
    [InlineData("{{ 7 | modulo: 3 }}", "1")]
    [InlineData("{{ -7 | modulo: -3 }}", "-1")]
    [InlineData("{{ 10.1 | modulo: 7.0 }}", "3.1")]
    [InlineData("{{ -6 | modulo: 3 }}", "0")]
    [InlineData("{{ -4 | modulo: 2.0 }}", "0.0")]
    public async Task TakesTheSignOfTheDivisor(string source, string expected)
    {
        Assert.Equal(expected, await _parser.Parse(source).RenderAsync(new TemplateContext()));
    }
}
