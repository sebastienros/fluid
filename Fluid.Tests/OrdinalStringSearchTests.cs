using System.Threading.Tasks;
using Xunit;

namespace Fluid.Tests;

/// <summary>
/// A culture-sensitive search ignores characters like the soft hyphen and the zero-width space, so
/// it reports them as found at the start of any string. These searches have to be ordinal.
/// </summary>
public class OrdinalStringSearchTests
{
    private const string SoftHyphen = "­";
    private const string ZeroWidthSpace = "​";

    private static readonly FluidParser _parser = new FluidParser();

    [Theory]
    // The character isn't in the text: nothing changes.
    [InlineData("{{ 'abc' | remove_first: x }}", SoftHyphen, "abc")]
    [InlineData("{{ 'abc' | remove_last: x }}", SoftHyphen, "abc")]
    [InlineData("{{ 'abc' | replace_first: x, '-' }}", SoftHyphen, "abc")]
    [InlineData("{{ 'abc' | replace_last: x, '-' }}", SoftHyphen, "abc")]
    [InlineData("{% if 'abc' startswith x %}yes{% else %}no{% endif %}", ZeroWidthSpace, "no")]
    [InlineData("{% if 'abc' endswith x %}yes{% else %}no{% endif %}", ZeroWidthSpace, "no")]
    public async Task ShouldNotMatchAnIgnorableCharacterThatIsAbsent(string source, string x, string expected)
    {
        var template = _parser.Parse(source);
        var context = new TemplateContext().SetValue("x", x);

        Assert.Equal(expected, await template.RenderAsync(context));
    }

    [Theory]
    // The character is in the text: that occurrence is the one affected.
    [InlineData("{{ s | remove_first: x }}", "abcdefabc")]
    [InlineData("{{ s | remove_last: x }}", "abcdefabc")]
    [InlineData("{{ s | replace_first: x, '-' }}", "abc-defabc")]
    [InlineData("{{ s | replace_last: x, '-' }}", "abc-defabc")]
    public async Task ShouldFindAnIgnorableCharacterWhereItIs(string source, string expected)
    {
        var template = _parser.Parse(source);
        var context = new TemplateContext()
            .SetValue("s", "abc" + ZeroWidthSpace + "defabc")
            .SetValue("x", ZeroWidthSpace);

        Assert.Equal(expected, await template.RenderAsync(context));
    }
}
