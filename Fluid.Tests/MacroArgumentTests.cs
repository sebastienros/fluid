using System.Threading.Tasks;
using Xunit;

namespace Fluid.Tests;

public class MacroArgumentTests
{
#if COMPILED
    private static readonly FluidParser _parser = new FluidParser(new FluidParserOptions { AllowFunctions = true }).Compile();
#else
    private static readonly FluidParser _parser = new FluidParser(new FluidParserOptions { AllowFunctions = true });
#endif

    private const string Macro = "{% macro greet(first, last='Smith') %}[{{ first }} {{ last }}]{% endmacro %}";

    [Theory]
    // More arguments than declared: the extra ones are ignored.
    [InlineData("{{ greet('Ann', 'Lee', 'extra') }}", "[Ann Lee]")]
    [InlineData("{{ greet('Ann', 'Lee', 'extra', 4, 5) }}", "[Ann Lee]")]
    [InlineData("{{ greet(first = 'Ann', last = 'Lee', other = 'x') }}", "[Ann Lee]")]
    // Unchanged cases.
    [InlineData("{{ greet('Ann', 'Lee') }}", "[Ann Lee]")]
    [InlineData("{{ greet('Ann') }}", "[Ann Smith]")]
    [InlineData("{{ greet(first = 'Ann') }}", "[Ann Smith]")]
    [InlineData("{{ greet() }}", "[ Smith]")]
    public async Task ExtraArgumentsAreIgnored(string call, string expected)
    {
        Assert.Equal(expected, await _parser.Parse(Macro + call).RenderAsync(new TemplateContext()));
    }

    [Fact]
    public async Task MacroWithoutParametersIgnoresArguments()
    {
        var template = _parser.Parse("{% macro hi() %}hi{% endmacro %}{{ hi('a', 'b') }}");

        Assert.Equal("hi", await template.RenderAsync(new TemplateContext()));
    }
}
