using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Xunit;

namespace Fluid.Tests;

public class IntegerKeyedDictionaryTests
{
    private static readonly FluidParser _parser = new FluidParser();

    private static async Task<string> RenderAsync(string source, object dictionary, CultureInfo cultureInfo = null)
    {
        var options = new TemplateOptionsBuilder()
            .WithCultureInfo(cultureInfo ?? CultureInfo.InvariantCulture)
            .Build();
        var context = new TemplateContext(options);
        context.SetValue("d", dictionary);

        return await _parser.Parse(source).RenderAsync(context);
    }

    [Theory]
    [InlineData("{{ d[5] }}", "five")]
    [InlineData("{{ d['5'] }}", "five")]
    [InlineData("{{ d[-3] }}", "minus three")]
    [InlineData("[{{ d[6] }}]", "[]")]
    // Not how a key is written, so not a key.
    [InlineData("[{{ d['05'] }}]", "[]")]
    [InlineData("[{{ d['+5'] }}]", "[]")]
    [InlineData("[{{ d[' 5'] }}]", "[]")]
    [InlineData("[{{ d['five'] }}]", "[]")]
    [InlineData("[{{ d['99999999999999999999'] }}]", "[]")]
    [InlineData("{{ d.size }}", "2")]
    public async Task ShouldResolveIntKeys(string source, string expected)
    {
        var dictionary = new Dictionary<int, string> { [5] = "five", [-3] = "minus three" };

        Assert.Equal(expected, await RenderAsync(source, dictionary));
    }

    [Fact]
    public async Task ShouldResolveOtherIntegerKeyTypes()
    {
        Assert.Equal("a", await RenderAsync("{{ d[5] }}", new Dictionary<long, string> { [5] = "a" }));
        Assert.Equal("b", await RenderAsync("{{ d[5] }}", new Dictionary<byte, string> { [5] = "b" }));
        Assert.Equal("c", await RenderAsync("{{ d[5] }}", new Dictionary<short, string> { [5] = "c" }));
        Assert.Equal("d", await RenderAsync("{{ d[5] }}", new Dictionary<uint, string> { [5] = "d" }));
        Assert.Equal("e", await RenderAsync("{{ d['18446744073709551615'] }}", new Dictionary<ulong, string> { [ulong.MaxValue] = "e" }));

        // Out of the range of the key type.
        Assert.Equal("[]", await RenderAsync("[{{ d[300] }}]", new Dictionary<byte, string> { [5] = "b" }));
        Assert.Equal("[]", await RenderAsync("[{{ d[-1] }}]", new Dictionary<uint, string> { [5] = "d" }));
    }

    [Fact]
    public async Task ShouldUseTheCultureToWriteKeys()
    {
        var cultureInfo = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        cultureInfo.NumberFormat.NegativeSign = "~";

        var dictionary = new Dictionary<int, string> { [-3] = "minus three" };

        Assert.Equal("minus three", await RenderAsync("{{ d['~3'] }}", dictionary, cultureInfo));
        Assert.Equal("[]", await RenderAsync("[{{ d['-3'] }}]", dictionary, cultureInfo));
    }

    [Fact]
    public async Task ShouldStillResolveKeysOfMixedTypes()
    {
        var dictionary = new Hashtable { [5] = "int", [7L] = "long", ["name"] = "string" };

        Assert.Equal("int long string", await RenderAsync("{{ d[5] }} {{ d[7] }} {{ d.name }}", dictionary));
    }

    private enum Color
    {
        Red,
        Blue
    }

    [Fact]
    public async Task ShouldStillResolveEnumKeys()
    {
        var dictionary = new Dictionary<Color, string> { [Color.Blue] = "blue" };

        Assert.Equal("blue", await RenderAsync("{{ d.Blue }}", dictionary));
    }
}
