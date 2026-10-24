using System.Threading.Tasks;
using Xunit;

namespace Fluid.Tests;

public class EscapeFilterTests
{
#if COMPILED
    private static readonly FluidParser _parser = new FluidParser().Compile();
#else
    private static readonly FluidParser _parser = new FluidParser();
#endif

    [Theory]
    // Only & < > " and ' are escaped.
    [InlineData("{{ 'café' | escape }}", "café")]
    [InlineData("{{ 'Grüße ñ © ½ ÿ' | escape }}", "Grüße ñ © ½ ÿ")]
    [InlineData("{{ '😀 <' | escape }}", "😀 &lt;")]
    [InlineData("{{ \"<b>it's & co</b>\" | escape }}", "&lt;b&gt;it&#39;s &amp; co&lt;/b&gt;")]
    [InlineData("{{ '\"x\"' | escape }}", "&quot;x&quot;")]
    [InlineData("{{ 'café' | escape_once }}", "café")]
    [InlineData("{{ 'café &amp; <b>' | escape_once }}", "café &amp; &lt;b&gt;")]
    // escape_once keeps named and decimal character references as they are.
    [InlineData("{{ '&copy; &nbsp; &hellip; &#233;' | escape_once }}", "&copy; &nbsp; &hellip; &#233;")]
    [InlineData("{{ '&lt;&LT;&Amp;' | escape_once }}", "&lt;&LT;&Amp;")]
    [InlineData("{{ '&amp;&lt;&gt;&quot;&#39;' | escape_once }}", "&amp;&lt;&gt;&quot;&#39;")]
    [InlineData("{{ '&#x27; &foo; & &1; &#;' | escape_once }}", "&amp;#x27; &foo; &amp; &amp;1; &amp;#;")]
    [InlineData("{{ '&amp' | escape_once }}", "&amp;amp")]
    public async Task EscapesOnlyHtmlSpecialCharacters(string source, string expected)
    {
        Assert.Equal(expected, await _parser.Parse(source).RenderAsync(new TemplateContext()));
    }
}
