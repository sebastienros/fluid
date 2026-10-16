using System.Threading.Tasks;
using Xunit;

namespace Fluid.Tests;

/// <summary>
/// A capture runs its whole body, even when every statement in it writes nothing or only whitespace.
/// </summary>
public class CaptureBodyTests
{
#if COMPILED
    private static readonly FluidParser _parser = new FluidParser().Compile();
#else
    private static readonly FluidParser _parser = new FluidParser();
#endif

    private static Task<string> RenderAsync(string source) =>
        _parser.Parse(source).RenderAsync(new TemplateContext()).AsTask();

    [Theory]
    [InlineData("{% capture x %}{% increment a %}{% endcapture %}[{{ x }}]{% increment a %}", "[0]1")]
    [InlineData("{% capture x %}{% decrement a %}{% endcapture %}[{{ x }}]{% decrement a %}", "[-1]-2")]
    [InlineData("{% capture x %}{% cycle 'a', 'b' %}{% endcapture %}[{{ x }}]{% cycle 'a', 'b' %}", "[a]b")]
    [InlineData("{% capture x %}{% increment a %}{% increment a %}{% endcapture %}[{{ x }}]", "[01]")]
    [InlineData("{% capture x %} {% increment a %} {% endcapture %}[{{ x }}]", "[ 0 ]")]
    public async Task CaptureRendersTheTags(string source, string expected)
    {
        Assert.Equal(expected, await RenderAsync(source));
    }

    [Theory]
    [InlineData("{% capture x %}{% assign y = 2 %}{% endcapture %}[{{ x }}{{ y }}]", "[2]")]
    [InlineData("{% capture x %}  {% endcapture %}[{{ x }}]", "[  ]")]
    [InlineData("{% capture x %}{% endcapture %}[{{ x }}]", "[]")]
    [InlineData("{% capture x %}{% comment %}c{% endcomment %}{% endcapture %}[{{ x }}]", "[]")]
    public async Task OtherBodiesAreRun(string source, string expected)
    {
        Assert.Equal(expected, await RenderAsync(source));
    }
}
