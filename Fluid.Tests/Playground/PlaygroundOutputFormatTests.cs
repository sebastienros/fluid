using System.Linq;
using System.Threading.Tasks;
using Fluid.Playground;
using Xunit;

namespace Fluid.Tests.Playground;

public class PlaygroundOutputFormatTests
{
    [Theory]
    [InlineData("""{ "a": [1, 2] }""")]
    [InlineData("""  [ { "a": null } ]  """)]
    [InlineData("[]")]
    public void DetectsJson(string output) => Assert.Equal(PlaygroundLanguage.Json, PlaygroundOutputFormat.Detect(output));

    [Theory]
    [InlineData("""<?xml version="1.0"?><a/>""")]
    [InlineData("<rss version=\"2.0\"><channel><title>T</title></channel></rss>\n")]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"><circle r=\"1\"/></svg>")]
    [InlineData("<ns:root><ns:item/></ns:root>")]
    public void DetectsXml(string output) => Assert.Equal(PlaygroundLanguage.Xml, PlaygroundOutputFormat.Detect(output));

    [Theory]
    [InlineData("<!DOCTYPE html><html><body></body></html>")]
    [InlineData("<ul>\n  <li>Apple</li>\n</ul>")]
    [InlineData("Hello <b>world</b>!")]
    [InlineData("<div class=\"card\">A</div><div>B</div>")]
    [InlineData("Line<br>Line")]
    public void DetectsHtml(string output) => Assert.Equal(PlaygroundLanguage.Html, PlaygroundOutputFormat.Detect(output));

    [Theory]
    [InlineData("using System;\n\nConsole.WriteLine(1);")]
    [InlineData("namespace Contoso.Store;\n")]
    [InlineData("public sealed partial class Product\n{\n}")]
    [InlineData("// Generated\n[Serializable]\ninternal record Order(int Id);")]
    [InlineData("/// <summary>A product.</summary>\npublic record Product(string Name);")]
    [InlineData("public enum Color { Red, Blue }")]
    public void DetectsCSharp(string output) => Assert.Equal(PlaygroundLanguage.CSharp, PlaygroundOutputFormat.Detect(output));

    [Theory]
    [InlineData("# Title\n\nSome text.")]
    [InlineData("Intro\n\n```js\nlet a = 1;\n```")]
    [InlineData("- one\n- two\n\nSee [the docs](https://example.com).")]
    [InlineData("> **Note**: quoted")]
    [InlineData("| a | b |\n| --- | --- |\n| 1 | 2 |\n\nUse `code`.")]
    [InlineData("## Order\n\nLine<br>Line")]
    public void DetectsMarkdown(string output) => Assert.Equal(PlaygroundLanguage.Markdown, PlaygroundOutputFormat.Detect(output));

    [Theory]
    [InlineData("")]
    [InlineData("   \n ")]
    [InlineData("Hello World")]
    [InlineData("{ not json }")]
    [InlineData("[1, 2")]
    [InlineData("a < b and c > d")]
    [InlineData("List<string> items")]
    [InlineData("1. Apple: 3.95")]
    [InlineData("Price: 3 * 4 = 12")]
    [InlineData("#hashtag and more")]
    [InlineData("We are using the products, namespace or not.")]
    [InlineData("Public class schedule: Monday")]
    public void PlainTextIsNotColored(string output) => Assert.Null(PlaygroundOutputFormat.Detect(output));

    [Fact]
    public void LongOutputsAreNotDetected()
    {
        Assert.Null(PlaygroundOutputFormat.Detect("<b>" + new string('x', PlaygroundOutputFormat.MaxLength) + "</b>"));
    }

    [Theory]
    [InlineData("products", PlaygroundLanguage.Html)]
    [InlineData("json", PlaygroundLanguage.Json)]
    [InlineData("xml", PlaygroundLanguage.Xml)]
    [InlineData("markdown", PlaygroundLanguage.Markdown)]
    [InlineData("csharp", PlaygroundLanguage.CSharp)]
    [InlineData("liquid-tag", null)]
    public async Task DetectsTheFormatOfSamples(string id, PlaygroundLanguage? expected)
    {
        var result = await new PlaygroundEngine().RenderAsync(PlaygroundSamples.All.Single(s => s.Id == id).State);

        Assert.Null(result.Error);
        Assert.Equal(expected, PlaygroundOutputFormat.Detect(result.Output));
    }

    [Theory]
    [InlineData("<?xml version=\"1.0\"?><a b=\"c\">d</a>", PlaygroundLanguage.Xml, "a")]
    [InlineData("<p class=\"x\">y</p>", PlaygroundLanguage.Html, "p")]
    [InlineData("public record Product(string Name);", PlaygroundLanguage.CSharp, "record")]
    public void HighlightsOutputs(string output, PlaygroundLanguage language, string tagName)
    {
        var html = new PlaygroundHighlighter().ToHtml(output, language);

        Assert.Contains($">{tagName}</span>", html);
    }

    [Fact]
    public void HighlightsMarkdown()
    {
        var html = new PlaygroundHighlighter().ToHtml("# Title\n\n- **bold** and `code`\n\n```csharp\nvar a = 1;\n```", PlaygroundLanguage.Markdown);

        Assert.Contains("># Title</span>", html);
        Assert.Contains(">var</span>", html);
    }
}
