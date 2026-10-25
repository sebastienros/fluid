using System.Collections.Generic;
using System.Threading.Tasks;
using Fluid.Values;
using Xunit;

namespace Fluid.Tests;

/// <summary>
/// A capture runs its whole body, even when every statement in it writes nothing or only whitespace,
/// and tags that write output keep the whitespace around them in blocks.
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

    [Theory]
    [InlineData("{% for i in (1..3) %}{% capture x %}{% break %}{% endcapture %}{{ i }}{% endfor %}", "")]
    [InlineData("{% for i in (1..3) %}{% capture x %}{% continue %}{% endcapture %}{{ i }}{% endfor %}", "")]
    [InlineData("{% for i in (1..3) %}{% capture x %}{{ i }}{% if i == 2 %}{% break %}{% endif %}{% endcapture %}{{ x }}{% endfor %}", "1")]
    public async Task BreakAndContinueLeaveTheCapture(string source, string expected)
    {
        Assert.Equal(expected, await RenderAsync(source));
    }

    [Theory]
    [InlineData("{% if true %} {% increment a %} {% endif %}", " 0 ")]
    [InlineData("{% unless false %} {% decrement a %} {% endunless %}", " -1 ")]
    [InlineData("{% case 1 %}{% when 1 %} {% cycle 'a', 'b' %} {% endcase %}", " a ")]
    [InlineData("{% if true %} {% assign a = 1 %} {% endif %}", "")]
    public async Task OutputTagsKeepTheBlockWhitespace(string source, string expected)
    {
        Assert.Equal(expected, await RenderAsync(source));
    }

    [Theory]
    [InlineData("{% capture x %}{% endcapture %}", "")]
    [InlineData("{% capture x %}  {% endcapture %}", "  ")]
    [InlineData("{% capture x %}{% comment %}c{% endcomment %}{% endcapture %}", "")]
    [InlineData("{% capture x %}{% increment a %}{% endcapture %}", "0")]
    public async Task CapturedCallbackRunsForEveryCapture(string source, string expected)
    {
        var calls = new List<string>();
        var options = new TemplateOptionsBuilder()
            .WithCaptured((identifier, value, context) =>
            {
                calls.Add(identifier + "=" + value.ToStringValue(context));
                return new ValueTask<FluidValue>(StringValue.Create("replaced"));
            })
            .Build();

        var result = await _parser.Parse(source + "[{{ x }}]").RenderAsync(new TemplateContext(options));

        Assert.Equal(["x=" + expected], calls);
        Assert.Equal("[replaced]", result);
    }
}
