using System;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Fluid.Values;
using Xunit;

namespace Fluid.Tests;

public class StringValueTests
{
    [Theory]
    [InlineData("", "", true)]
    [InlineData("Foo", "Foo", false)]
    public void CreateStringValue(string value, string expected, bool isEmpty)
    {
        // Arrange & Act
        var stringValue = StringValue.Create(value);

        // Assert
        Assert.Equal(expected, stringValue.ToStringValue());
        Assert.Equal(isEmpty, stringValue.Equals(StringValue.Empty));
    }

    [Fact]
    public void StringValueCreateNullShouldReturnNil()
    {
        var stringValue = StringValue.Create(null);

        Assert.Same(NilValue.Instance, stringValue);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void StringValue_Create_InitializesProperties(bool encode)
    {
        var stringValue = Assert.IsType<StringValue>(StringValue.Create("a", encode));

        // Assert
        Assert.Equal("a", stringValue.ToStringValue());
        Assert.Equal(encode, stringValue.Encode);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EncodedFactoryNullShouldReturnNil(bool encode)
    {
        Assert.Same(NilValue.Instance, StringValue.Create(null, encode));
    }

    [Fact]
    public void EncodedFactoryShouldReuseCachedValues()
    {
        Assert.Same(StringValue.Empty, StringValue.Create(""));
        Assert.Same(StringValue.Empty, StringValue.Create("", true));
        Assert.Same(StringValue.Space, StringValue.Create(" "));

        for (var i = 0; i < 256; i++)
        {
            var text = ((char)i).ToString();
            var value = StringValue.Create(text);
            Assert.Same(value, StringValue.Create(text));
            Assert.Same(value, StringValue.Create(text, true));
            Assert.Same(value, FluidValue.Create(text, TemplateOptions.Default));
            Assert.Equal(text, value.ToStringValue());
            Assert.True(Assert.IsType<StringValue>(value).Encode);
        }
    }

    [Theory]
    [InlineData("\u0100")]
    [InlineData("hello")]
    public void UncachedStringsShouldPreserveText(string text)
    {
        var value = StringValue.Create(text);
        Assert.Equal(text, value.ToStringValue());
        Assert.NotSame(value, StringValue.Create(text));
    }

    [Theory]
    [InlineData("")]
    [InlineData("&")]
    [InlineData("<b>hello</b>")]
    public async Task RawStringsShouldNotAffectEncodedStrings(string text)
    {
        var encoded = Assert.IsType<StringValue>(StringValue.Create(text));
        var raw = Assert.IsType<StringValue>(StringValue.Create(text, false));

        Assert.NotSame(encoded, raw);
        Assert.True(encoded.Encode);
        Assert.False(raw.Encode);
        Assert.False(typeof(StringValue).GetProperty(nameof(StringValue.Encode)).CanWrite);

        var template = new FluidParser().Parse("{{ encoded }}|{{ raw }}|{{ raw | upcase }}");
        var context = new TemplateContext().SetValue("encoded", encoded).SetValue("raw", raw);
        Assert.Equal(HtmlEncoder.Default.Encode(text) + "|" + text + "|" + text.ToUpperInvariant(),
            await template.RenderAsync(context, HtmlEncoder.Default));
        Assert.True(Assert.IsType<StringValue>(StringValue.Create(text)).Encode);
    }
}
