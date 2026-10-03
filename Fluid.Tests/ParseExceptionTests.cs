using Parlot;
using System;
using Xunit;

namespace Fluid.Tests;

public class ParseExceptionTests
{
    [Fact]
    public void ExistingConstructorsLeaveMetadataUnavailable()
    {
        var inner = new InvalidOperationException("Inner error");
        var exceptions = new[]
        {
            new ParseException(),
            new ParseException("Parse error"),
            new ParseException("Parse error", inner)
        };

        foreach (var exception in exceptions)
        {
            Assert.Null(exception.TemplateSource);
            Assert.Null(exception.Position);
        }

        Assert.Equal("Parse error", exceptions[1].Message);
        Assert.Equal("Parse error", exceptions[2].Message);
        Assert.Same(inner, exceptions[2].InnerException);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MetadataConstructorsPreserveValues(bool withInnerException)
    {
        var position = new TextPosition(10, 2, 3);
        var inner = new InvalidOperationException("Inner error");
        var exception = withInnerException
            ? new ParseException("Parse error", "full template", position, inner)
            : new ParseException("Parse error", "full template", position);

        Assert.Equal("Parse error", exception.Message);
        Assert.Equal("full template", exception.TemplateSource);
        Assert.Equal(position, exception.Position);
        Assert.Same(withInnerException ? inner : null, exception.InnerException);
        exception.Source = "Originating assembly";
        Assert.Equal("Originating assembly", exception.Source);
        Assert.Equal("full template", exception.TemplateSource);
    }

    [Fact]
    public void MetadataConstructorAllowsUnavailablePosition()
    {
        var exception = new ParseException("Parse error", "full template", null);

        Assert.Equal("full template", exception.TemplateSource);
        Assert.Null(exception.Position);
    }
}
