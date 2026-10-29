using System;
using System.Threading.Tasks;
using Xunit;

namespace Fluid.Tests;

public class MaxStepsMessageTests
{
#if COMPILED
    private static readonly FluidParser _parser = new FluidParser().Compile();
#else
    private static readonly FluidParser _parser = new FluidParser();
#endif

    [Fact]
    public async Task ExceedingMaxStepsSaysSo()
    {
        var options = new TemplateOptionsBuilder().WithMaxSteps(10).Build();
        var template = _parser.Parse("{% for i in (1..100) %}{{ i }}{% endfor %}");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => template.RenderAsync(new TemplateContext(options)).AsTask());

        Assert.Equal("The maximum number of 10 steps has been reached.", exception.Message);
    }

    [Fact]
    public void MaxStepsAllowsExactlyThatManySteps()
    {
        var context = new TemplateContext { MaxSteps = 3 };

        context.IncrementSteps();
        context.IncrementSteps();
        context.IncrementSteps();

        Assert.Throws<InvalidOperationException>(context.IncrementSteps);
    }

    [Fact]
    public async Task ExceedingMaxRecursionStillSaysSo()
    {
        var options = new TemplateOptionsBuilder().WithMaxRecursion(3).Build();
        var template = _parser.Parse("{% for a in (1..1) %}{% for b in (1..1) %}{% for c in (1..1) %}{% for d in (1..1) %}x{% endfor %}{% endfor %}{% endfor %}{% endfor %}");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => template.RenderAsync(new TemplateContext(options), NullEncoder.Default, isolateContext: false).AsTask());

        Assert.StartsWith("The maximum level of recursion has been reached.", exception.Message);
    }
}
