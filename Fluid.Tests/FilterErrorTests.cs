using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Fluid.Tests;

public class FilterErrorTests
{
    private static readonly FluidParser _parser = new FluidParser();

    [Theory]
    [InlineData("{{ 1 | divided_by: 0 }}", "divided by 0")]
    [InlineData("{{ 1.5 | divided_by: 0.0 }}", "divided by 0")]
    [InlineData("{{ 5 | divided_by: nil }}", "divided by 0")]
    [InlineData("{{ 5 | modulo: 0 }}", "divided by 0")]
    [InlineData("{{ 5 | modulo: 'x' }}", "divided by 0")]
    [InlineData("{{ 'QQ' | base64_decode }}", "invalid base64 provided to base64_decode")]
    [InlineData("{{ 'abc' | slice: nil }}", "Slice: offset argument is an invalid number")]
    [InlineData("{{ 'abc' | slice: 1, 'x' }}", "Slice: length argument is not a number")]
    [InlineData("{{ 'abc' | truncate: nil }}", "truncate: cannot convert nil to number")]
    [InlineData("{{ 'a b c' | truncatewords: nil }}", "truncatewords: cannot convert nil to number")]
    [InlineData("{{ 9 | compact | reject: '' }}", "Cannot access property '' on Number")]
    [InlineData("{{ 79228162514264337593543950335 | plus: 1 }}", "plus: the result is too large to be represented")]
    [InlineData("{{ -79228162514264337593543950335 | minus: 1 }}", "minus: the result is too large to be represented")]
    [InlineData("{{ 79228162514264337593543950335 | times: 2 }}", "times: the result is too large to be represented")]
    [InlineData("{{ 79228162514264337593543950335 | divided_by: 0.5 }}", "divided_by: the result is too large to be represented")]
    public async Task InvalidFilterInputsThrowLiquidException(string source, string message)
    {
        var template = _parser.Parse(source);

        var exception = await Assert.ThrowsAsync<LiquidException>(() => template.RenderAsync(new TemplateContext()).AsTask());

        Assert.Equal(message, exception.Message);
    }

    [Fact]
    public async Task SortingIncompatibleTypesThrowsLiquidException()
    {
        var context = new TemplateContext();
        context.SetValue("items", new object[] { 1, new Dictionary<string, object>() });

        await Assert.ThrowsAsync<LiquidException>(() => _parser.Parse("{{ items | sort }}").RenderAsync(context).AsTask());
    }
}
