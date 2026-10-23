using System.Threading.Tasks;
using Xunit;

namespace Fluid.Tests;

/// <summary>
/// Liquid starts a negative offset at the first item, and ends the slice at offset + limit.
/// </summary>
public class NegativeLoopOffsetTests
{
#if COMPILED
    private static readonly FluidParser _parser = new FluidParser().Compile();
#else
    private static readonly FluidParser _parser = new FluidParser();
#endif

    private static Task<string> RenderAsync(string source) =>
        _parser.Parse(source).RenderAsync(new TemplateContext().SetValue("items", new[] { 1, 2, 3 })).AsTask();

    [Theory]
    [InlineData("{% for i in items offset: -1 %}{{ i }}{% endfor %}", "123")]
    [InlineData("{% for i in items offset: -100 %}{{ i }}{% endfor %}", "123")]
    [InlineData("{% for i in items offset: -1 limit: 2 %}{{ i }}{% endfor %}", "1")]
    [InlineData("{% for i in items offset: -1 limit: 10 %}{{ i }}{% endfor %}", "123")]
    [InlineData("{% for i in items offset: -2 limit: 2 %}{{ i }}{% else %}none{% endfor %}", "none")]
    [InlineData("{% for i in items offset: -1 reversed %}{{ i }}{% endfor %}", "321")]
    [InlineData("{% for i in items offset: '-1' %}{{ i }}{% endfor %}", "123")]
    // Unchanged.
    [InlineData("{% for i in items offset: 1 limit: 1 %}{{ i }}{% endfor %}", "2")]
    [InlineData("{% for i in items offset: 0 %}{{ i }}{% endfor %}", "123")]
    public async Task ForLoop(string source, string expected)
    {
        Assert.Equal(expected, await RenderAsync(source));
    }

    [Theory]
    [InlineData("{% tablerow i in items offset: -1 %}{{ i }}{% endtablerow %}", "<tr class=\"row1\">\n<td class=\"col1\">1</td><td class=\"col2\">2</td><td class=\"col3\">3</td></tr>\n")]
    [InlineData("{% tablerow i in items offset: -1 limit: 2 %}{{ i }}{% endtablerow %}", "<tr class=\"row1\">\n<td class=\"col1\">1</td></tr>\n")]
    [InlineData("{% tablerow i in items offset: -2 limit: 2 %}{{ i }}{% endtablerow %}", "")]
    public async Task TableRow(string source, string expected)
    {
        Assert.Equal(expected, await RenderAsync(source));
    }
}
