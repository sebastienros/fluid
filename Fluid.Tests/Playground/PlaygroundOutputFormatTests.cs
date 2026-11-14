using System;
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
    [InlineData("name: Fluid\nversion: 3")]
    [InlineData("Total: 3 products\nPrice: 6.78")]
    [InlineData("# Settings\nserver:\n  host: localhost\n  ports:\n    - 80\n    - 443\n")]
    [InlineData("---\ntitle: Hello\ntags: [a, b]\n")]
    [InlineData("- name: Apple\n  price: 3.29\n- name: Orange\n  price: 2.5")]
    [InlineData("description: |\n  A long\n  text\n\"quoted key\": 1")]
    public void DetectsYaml(string output) => Assert.Equal(PlaygroundLanguage.Yaml, PlaygroundOutputFormat.Detect(output));

    [Theory]
    [InlineData("body { color: red; }")]
    [InlineData(":root {\n  --primary: #1693e6;\n}\n\n.text-primary { color: var(--primary); }")]
    [InlineData("@media (max-width: 600px) {\n  .a { display: none; }\n}")]
    [InlineData("h1, h2 > a:hover\n{\n  font-weight: bold\n}")]
    public void DetectsCss(string output) => Assert.Equal(PlaygroundLanguage.Css, PlaygroundOutputFormat.Detect(output));

    [Theory]
    [InlineData("---\ntitle: Hello\n---\n\nSome content.")]
    [InlineData("---\ntitle: Hello\n---\n# Heading")]
    public void FrontMatterWithContentIsMarkdown(string output) => Assert.Equal(PlaygroundLanguage.Markdown, PlaygroundOutputFormat.Detect(output));

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
    [InlineData("Parsed in the time zone: Fri\nConverted: Sat")]
    [InlineData("interface Product { name: string; }")]
    [InlineData("const theme = { color: 'red' };")]
    [InlineData("if (a) { b: c; }")]
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
    [InlineData("yaml", PlaygroundLanguage.Yaml)]
    [InlineData("css", PlaygroundLanguage.Css)]
    [InlineData("time-zones", null)]
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

    public static TheoryData<string> FormatIds() => new(PlaygroundFormats.All.Select(f => f.Id));

    [Theory]
    [MemberData(nameof(FormatIds))]
    public void EveryFormatHighlights(string id)
    {
        var format = PlaygroundFormats.Find(id);
        var highlighter = new PlaygroundHighlighter();

        var html = highlighter.ToHtml("a = 1 # \"x\" <b> { c: d }", format.Language);

        Assert.StartsWith("<pre class=\"tint\"", html);
    }

    [Theory]
    [InlineData(PlaygroundLanguage.Liquid)]
    [InlineData(PlaygroundLanguage.Yaml)]
    [InlineData(PlaygroundLanguage.Php)]
    [InlineData(PlaygroundLanguage.Html)]
    public void LanguagesHaveTheGrammarsTheyNeed(PlaygroundLanguage language)
    {
        // Their own grammar parts are registered. What can be missing is optional: embedded languages, and the YAML 1.0,
        // 1.1 and 1.3 grammars, only used after a %YAML directive.
        var missing = new PlaygroundHighlighter().GetMissingGrammars(language);

        Assert.DoesNotContain(missing, s => s is "source.yaml.1.2" or "source.yaml.embedded" or "source.php" or "text.html.derivative" or "text.html.basic" or "source.css" or "source.js");
    }

    [Fact]
    public void DetectedFormatsAreInThePicker()
    {
        foreach (var language in new[] { PlaygroundLanguage.Html, PlaygroundLanguage.Json, PlaygroundLanguage.Xml, PlaygroundLanguage.Markdown, PlaygroundLanguage.Yaml, PlaygroundLanguage.Css, PlaygroundLanguage.CSharp })
        {
            Assert.True(PlaygroundFormats.All.Single(f => f.Language == language).Detected);
        }
    }
}
