using System;
using System.Threading.Tasks;
using Xunit;

namespace Fluid.Tests;

public class StrictFiltersTests
{
#if COMPILED
    private static readonly FluidParser _parser = new FluidParser().Compile();
#else
    private static readonly FluidParser _parser = new FluidParser();
#endif

    [Fact]
    public async Task UnknownFilter_DefaultBehavior_ReturnsInput()
    {
        _parser.TryParse("{{ 'hello' | unknown }}", out var template, out var _);
        var context = new TemplateContext();
        var result = await template.RenderAsync(context);
        Assert.Equal("hello", result);
    }

    [Fact]
    public async Task UnknownFilter_StrictFilters_Throws()
    {
        _parser.TryParse("{{ 'hello' | unknown }}", out var template, out var _);
        var options = new TemplateOptions { StrictFilters = true };
        var context = new TemplateContext(options);
        await Assert.ThrowsAsync<FluidException>(() => template.RenderAsync(context).AsTask());
    }

    [Fact]
    public async Task KnownFilter_StrictFilters_Succeeds()
    {
        _parser.TryParse("{{ 'hello' | upcase }}", out var template, out var _);
        var options = new TemplateOptions { StrictFilters = true };
        var context = new TemplateContext(options);
        var result = await template.RenderAsync(context);
        Assert.Equal("HELLO", result);
    }
}