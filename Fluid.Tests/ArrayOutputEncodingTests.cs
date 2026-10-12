using Fluid.Values;
using System.Collections.Generic;
using System.Linq;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Xunit;

namespace Fluid.Tests;

public class ArrayOutputEncodingTests
{
    private const string Markup = "<b>x</b>";
    private const string Encoded = "&lt;b&gt;x&lt;/b&gt;";

    private static readonly FluidParser _parser = new FluidParser();

    private static async Task<string> RenderAsync(string source, object items, TextEncoder encoder = null)
    {
        var context = new TemplateContext();
        context.SetValue("items", items);

        return await _parser.Parse(source).RenderAsync(context, encoder ?? HtmlEncoder.Default);
    }

    [Fact]
    public async Task ArrayItemsAreEncoded()
    {
        Assert.Equal(Encoded + "a", await RenderAsync("{{ items }}", new[] { Markup, "a" }));
    }

    [Fact]
    public async Task ArrayIsEncodedLikeItsItemsAre()
    {
        var items = new[] { Markup };

        var whole = await RenderAsync("{{ items }}", items);

        Assert.Equal(whole, await RenderAsync("{{ items[0] }}", items));
        Assert.Equal(whole, await RenderAsync("{{ items | join: '' }}", items));
        Assert.Equal(whole, await RenderAsync("{% for item in items %}{{ item }}{% endfor %}", items));
    }

    [Fact]
    public async Task EnumerableItemsAreEncoded()
    {
        // Not a list, so it is wrapped as an enumerable object rather than an array value.
        var items = new[] { Markup, "a" }.Where(x => x.Length > 0);

        Assert.Equal(Encoded + "a", await RenderAsync("{{ items }}", items));
    }

    [Fact]
    public async Task NestedArrayItemsAreEncoded()
    {
        var items = new object[] { new[] { Markup }, new List<string> { "a", Markup } };

        Assert.Equal(Encoded + "a" + Encoded, await RenderAsync("{{ items }}", items));
    }

    [Fact]
    public async Task RawItemsAreNotEncoded()
    {
        var items = ArrayValue.Create(new[]
        {
            StringValue.Create(Markup, encode: false),
            StringValue.Create(Markup)
        });

        Assert.Equal(Markup + Encoded, await RenderAsync("{{ items }}", items));
    }

    [Fact]
    public async Task OtherItemsAreWrittenAsBefore()
    {
        var items = new object[] { 1, 2.50m, true, null, "a" };

        Assert.Equal("12.50truea", await RenderAsync("{{ items }}", items));
    }

    [Fact]
    public async Task NothingIsEncodedWithoutAnEncoder()
    {
        Assert.Equal(Markup + "a", await RenderAsync("{{ items }}", new[] { Markup, "a" }, NullEncoder.Default));
    }
}
