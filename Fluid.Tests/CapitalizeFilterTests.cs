using System.Threading.Tasks;
using Xunit;

namespace Fluid.Tests;

public class CapitalizeFilterTests
{
#if COMPILED
    private static readonly FluidParser _parser = new FluidParser().Compile();
#else
    private static readonly FluidParser _parser = new FluidParser();
#endif

    [Theory]
    // The rest of the string is lower-cased.
    [InlineData("hELLO wORLD", "Hello world")]
    [InlineData("HELLO", "Hello")]
    [InlineData("élan VITAL", "Élan vital")]
    // Even when the first character isn't a letter.
    [InlineData("1ABC", "1abc")]
    [InlineData(" ABC", " abc")]
    // Unchanged cases.
    [InlineData("hello", "Hello")]
    [InlineData("Hello world", "Hello world")]
    [InlineData("h", "H")]
    [InlineData("", "")]
    public async Task CapitalizesTheFirstCharacterAndLowercasesTheRest(string input, string expected)
    {
        var context = new TemplateContext().SetValue("s", input);

        Assert.Equal($"[{expected}]", await _parser.Parse("[{{ s | capitalize }}]").RenderAsync(context));
    }
}
