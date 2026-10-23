using System;
using System.Threading.Tasks;
using Xunit;

namespace Fluid.Tests;

/// <summary>
/// MaxSteps counts statements, loop iterations and collection enumeration, not the characters a
/// string filter goes through.
/// </summary>
public class StringFilterStepTests
{
#if COMPILED
    private static readonly FluidParser _parser = new FluidParser().Compile();
#else
    private static readonly FluidParser _parser = new FluidParser();
#endif

    private static readonly string Text = string.Join(" ", new string('x', 500), new string('y', 500), "a,b,c", "z\nz");

    private static Task<string> RenderAsync(string source, int maxSteps)
    {
        var context = new TemplateContext { MaxSteps = maxSteps };
        context.SetValue("text", Text);

        return _parser.Parse(source).RenderAsync(context).AsTask();
    }

    [Theory]
    [InlineData("{{ text | newline_to_br | size }}")]
    [InlineData("{{ text | split: ' ' | size }}")]
    [InlineData("{{ text | split: '' | size }}")]
    [InlineData("{{ text | split: ',' | size }}")]
    [InlineData("{{ text | replace: 'x', 'xx' | size }}")]
    [InlineData("{{ text | truncatewords: 3 | size }}")]
    public async Task LongStringsFitASmallStepLimit(string source)
    {
        var result = await RenderAsync(source, maxSteps: 10);

        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task LoopsAreStillCounted()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => RenderAsync("{% for i in (1..100) %}{% endfor %}", maxSteps: 10));
    }

    [Fact]
    public async Task EnumeratingTheItemsOfASplitIsStillCounted()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => RenderAsync("{{ text | split: '' | join: '' }}", maxSteps: 10));
    }
}
