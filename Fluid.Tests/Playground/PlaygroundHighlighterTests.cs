using System.Linq;
using System.Text.RegularExpressions;
using Fluid.Playground;
using Xunit;

namespace Fluid.Tests.Playground;

public class PlaygroundHighlighterTests
{
    private readonly PlaygroundHighlighter _highlighter = new();

    // The text of each colored run, with the light and dark colors of its style.
    private static (string Text, string Style)[] Runs(string html) =>
        Regex.Matches(html, "<span style=\"([^\"]*)\">([^<]*)</span>").Select(m => (m.Groups[2].Value, m.Groups[1].Value)).ToArray();

    [Fact]
    public void ColorsLiquidTagsFiltersAndHtml()
    {
        var html = _highlighter.ToHtml("<h1 class=\"x\">{{ store | upcase }}</h1>\n{% for p in products %}{% endfor %}", PlaygroundLanguage.Liquid);
        var runs = Runs(html);

        Assert.Equal(2, Regex.Matches(html, "<span class=\"line\">").Count);
        Assert.Contains(runs, r => r.Text == "upcase");
        Assert.Contains(runs, r => r.Text == "for");
        Assert.Contains(runs, r => r.Text == "h1");
        Assert.All(runs, r => Assert.Contains("--tint-dark:", r.Style));
    }

    [Fact]
    public void ColorsCssAndJavaScriptInsideTemplates()
    {
        var runs = Runs(_highlighter.ToHtml("<style>a { color: red }</style><script>const a = 1;</script>", PlaygroundLanguage.Liquid));

        Assert.Contains(runs, r => r.Text == "color");
        Assert.Contains(runs, r => r.Text == "const");
    }

    [Fact]
    public void ColorsJson()
    {
        var runs = Runs(_highlighter.ToHtml("""{ "name": "Ada", "age": 36, "admin": true }""", PlaygroundLanguage.Json));

        Assert.Contains(runs, r => r.Text == "\"name\"");
        Assert.Contains(runs, r => r.Text == "36");
        Assert.Contains(runs, r => r.Text == "true");
        // Keys and string values have different colors.
        Assert.NotEqual(runs.First(r => r.Text == "\"name\"").Style, runs.First(r => r.Text == "\"Ada\"").Style);
    }

    [Fact]
    public void EscapesTheCode()
    {
        var html = _highlighter.ToHtml("{{ '<b>&' }}", PlaygroundLanguage.Liquid);

        Assert.DoesNotContain("<b>", html);
        Assert.Contains("&lt;b&gt;&amp;", html);
    }
}
