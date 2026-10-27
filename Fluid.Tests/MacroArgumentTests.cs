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

    [Theory]
    [InlineData("{{ m(c = 3) }}", "[|B|3]")]
    [InlineData("{{ m(b = 2) }}", "[|2|C]")]
    [InlineData("{{ m(1, c = 3) }}", "[1|B|3]")]
    [InlineData("{{ m(c = 3, a = 1) }}", "[1|B|3]")]
    [InlineData("{{ m(1, 2) }}", "[1|2|C]")]
    // An argument without a name after a named one has no position to fill.
    [InlineData("{{ m(b = 2, 9) }}", "[|2|C]")]
    public async Task NamedArgumentsGoToTheirParameter(string call, string expected)
    {
        const string macro = "{% macro m(a, b='B', c='C') %}[{{ a }}|{{ b }}|{{ c }}]{% endmacro %}";

        Assert.Equal(expected, await _parser.Parse(macro + call).RenderAsync(new TemplateContext()));
    }

    [Fact]
    public async Task NamedArgumentIsNotAssignedToTheFirstParameter()
    {
        var template = _parser.Parse("{% macro greet(first, last) %}[{{ first }} {{ last }}]{% endmacro %}{{ greet(last = 'Lee') }}");

        Assert.Equal("[ Lee]", await template.RenderAsync(new TemplateContext()));
    }

    [Fact]
    public async Task MacroWithoutParametersIgnoresArguments()
    {
        var template = _parser.Parse("{% macro hi() %}hi{% endmacro %}{{ hi('a', 'b') }}");

        Assert.Equal("hi", await template.RenderAsync(new TemplateContext()));
    }
}
