using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Fluid.Tests;

public class TypedKeyDictionaryTests
{
    private static readonly FluidParser _parser = new FluidParser();
    private static readonly Guid Known = new Guid("0f8fad5b-d9cb-469f-a165-70867728950e");

    private enum Color
    {
        Red,
        Green,
        Blue
    }

    [Flags]
    private enum Access
    {
        None = 0,
        Read = 1,
        Write = 2
    }

    private static async Task<string> RenderAsync(string source, object dictionary, string key = null)
    {
        var context = new TemplateContext();
        context.SetValue("d", dictionary);
        context.SetValue("key", key);

        return await _parser.Parse(source).RenderAsync(context);
    }

    [Theory]
    [InlineData("0f8fad5b-d9cb-469f-a165-70867728950e", "[known]")]
    // Not how a Guid key is written, so not a key.
    [InlineData("0F8FAD5B-D9CB-469F-A165-70867728950E", "[]")]
    [InlineData("{0f8fad5b-d9cb-469f-a165-70867728950e}", "[]")]
    [InlineData("0f8fad5bd9cb469fa16570867728950e", "[]")]
    [InlineData("7c9e6679-7425-40de-944b-e07fc1f90ae7", "[]")]
    [InlineData("not a guid", "[]")]
    public async Task ShouldResolveGuidKeys(string key, string expected)
    {
        var dictionary = new Dictionary<Guid, string> { [Known] = "known", [Guid.Empty] = "empty" };

        Assert.Equal(expected, await RenderAsync("[{{ d[key] }}]", dictionary, key));
    }

    [Theory]
    [InlineData("a", "[first]")]
    [InlineData("b", "[]")]
    [InlineData("A", "[]")]
    [InlineData("ab", "[]")]
    public async Task ShouldResolveCharKeys(string key, string expected)
    {
        var dictionary = new Dictionary<char, string> { ['a'] = "first", ['z'] = "last" };

        Assert.Equal(expected, await RenderAsync("[{{ d[key] }}]", dictionary, key));
    }

    [Theory]
    [InlineData("Blue", "[blue]")]
    // Defined, but not in the dictionary.
    [InlineData("Green", "[]")]
    [InlineData("blue", "[]")]
    // A value without a name is written as its number.
    [InlineData("7", "[seven]")]
    [InlineData("8", "[]")]
    [InlineData("Purple", "[]")]
    public async Task ShouldResolveEnumKeys(string key, string expected)
    {
        var dictionary = new Dictionary<Color, string> { [Color.Red] = "red", [Color.Blue] = "blue", [(Color)7] = "seven" };

        Assert.Equal(expected, await RenderAsync("[{{ d[key] }}]", dictionary, key));
    }

    [Theory]
    [InlineData("Read", "[read]")]
    [InlineData("Read, Write", "[both]")]
    [InlineData("Write", "[]")]
    [InlineData("None", "[]")]
    public async Task ShouldResolveFlagsEnumKeys(string key, string expected)
    {
        var dictionary = new Dictionary<Access, string> { [Access.Read] = "read", [Access.Read | Access.Write] = "both" };

        Assert.Equal(expected, await RenderAsync("[{{ d[key] }}]", dictionary, key));
    }

    [Fact]
    public async Task ShouldStillExposeSizeAndMembers()
    {
        var dictionary = new Dictionary<Color, string> { [Color.Red] = "red", [Color.Blue] = "blue" };

        Assert.Equal("2 red", await RenderAsync("{{ d.size }} {{ d.Red }}", dictionary));
    }

    [Theory]
    [InlineData("{% if 'abcdef' startswith 'abc' %}yes{% else %}no{% endif %}", "yes")]
    [InlineData("{% if 'abcdef' startswith 'bcd' %}yes{% else %}no{% endif %}", "no")]
    [InlineData("{% if 'abcdef' endswith 'def' %}yes{% else %}no{% endif %}", "yes")]
    [InlineData("{% if 'abcdef' endswith 'cde' %}yes{% else %}no{% endif %}", "no")]
    [InlineData("{% if items startswith 'a' %}yes{% else %}no{% endif %}", "yes")]
    [InlineData("{% if items startswith 'c' %}yes{% else %}no{% endif %}", "no")]
    [InlineData("{% if items endswith 'c' %}yes{% else %}no{% endif %}", "yes")]
    [InlineData("{% if items endswith 'a' %}yes{% else %}no{% endif %}", "no")]
    [InlineData("{% if missing startswith 'a' %}yes{% else %}no{% endif %}", "no")]
    [InlineData("{% if 'abc' endswith missing %}yes{% else %}no{% endif %}", "yes")]
    public async Task StartsWithAndEndsWith(string source, string expected)
    {
        var context = new TemplateContext();
        context.SetValue("items", new[] { "a", "b", "c" });

        Assert.Equal(expected, await _parser.Parse(source).RenderAsync(context));
    }
}
