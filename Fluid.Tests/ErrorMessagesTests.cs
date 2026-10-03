using Fluid.Ast;
using Fluid.Parser;
using Parlot;
using Parlot.Fluent;
using System;
using System.Collections.Generic;
using Xunit;

namespace Fluid.Tests;

public class ErrorMessagesTests
{
#if COMPILED
    private static FluidParser _parser = new FluidParser().Compile();
#else
    private static FluidParser _parser = new FluidParser();
#endif

    [Theory]
    [InlineData("{% assign a 'b' %}", ErrorMessages.EqualAfterAssignIdentifier)]
    [InlineData("{% assign 'foo' %}", ErrorMessages.IdentifierAfterAssign)]
    [InlineData("{% assign %}", ErrorMessages.IdentifierAfterAssign)]
    [InlineData("{% assign a = | filter %}", ErrorMessages.LogicalExpressionStartsFilter)]
    [InlineData("{% assign a = %}", ErrorMessages.LogicalExpressionStartsFilter)]
    [InlineData("{% assign a = b | %}", ErrorMessages.IdentifierAfterPipe)]
    [InlineData("{% assign a = 1 | plus 2 %}", ErrorMessages.ExpectedTagEnd)]
    [InlineData("{{ 1 | plus 2 }}", ErrorMessages.ExpectedOutputEnd)]
    [InlineData("{% assing hello = 'world' %}", "Unknown tag 'assing' at (1:10)")]
    [InlineData("{% if true %} {{ foo }", ErrorMessages.ExpectedOutputEnd)]
    public void WrongTemplateShouldRenderErrorMessage(string source, string expected)
    {
        Assert.False(_parser.TryParse(source, out var _, out var error));
        Assert.StartsWith(expected, error); // e.g., message then 'at (1:15)'
    }

    [Theory]
    [InlineData("  {% assign a 'b' %}")]
    [InlineData("{% assign a = 'b' %}\n  {% assign a 'b' %}")]
    [InlineData("  {% assign a 'b' %}\n  {% assign a = 'b' %}")]
    [InlineData("  {% assign a 'b' %}\n\r  {% assign a = 'b' %}")]
    [InlineData("  {% assign a 'b' %}\n{% assign a = 'b' %}\n  {% assign a 'b' %}")]
    public void ErrorMessageContainsLineSource(string source)
    {
        Assert.False(_parser.TryParse(source, out var _, out var error));
        Assert.Contains("Source:\n  {% assign a 'b' %}", error);
    }

    [Theory]
    [InlineData("{% assign a 'b' %}", 11, 1, 12)]
    [InlineData("hello\n{% assign a 'b' %}", 17, 2, 12)]
    [InlineData("hello\r\n{% assign a 'b' %}", 18, 2, 12)]
    [InlineData("hello\r{% assign a 'b' %}", 17, 1, 17)]
    [InlineData("{% assing hello = 'world' %}", 9, 1, 10)]
    [InlineData("hello\n{% assing hello = 'world' %}", 15, 2, 10)]
    [InlineData("{% liquid\n  assing x\n%}", 18, 2, 9)]
    [InlineData("{% if true %}", 13, 1, 14)]
    [InlineData("{{", 2, 1, 3)]
    public void ParseExceptionContainsTemplateAndPosition(string source, int offset, int line, int column)
    {
        var exception = Assert.Throws<ParseException>(() => _parser.Parse(source));

        Assert.Equal(source, exception.TemplateSource);
        Assert.True(exception.Position.HasValue);
        Assert.Equal(offset, exception.Position.Value.Offset);
        Assert.Equal(line, exception.Position.Value.Line);
        Assert.Equal(column, exception.Position.Value.Column);
        Assert.False(_parser.TryParse(source, out var template, out var error));
        Assert.Null(template);
        Assert.Equal(exception.Message, error);
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    [InlineData("\r")]
    public void SourceExcerptDoesNotIncludeFollowingLine(string newline)
    {
        var exception = Assert.Throws<ParseException>(
            () => _parser.Parse($"hello{newline}  {{% assign a 'b' %}}{newline}following line"));

        Assert.EndsWith("Source:\n  {% assign a 'b' %}", exception.Message);
    }

    [Theory]
    [InlineData("invalid\nnext line", "invalid")]
    [InlineData("", "")]
    public void ErrorAtStartContainsFirstLine(string source, string excerpt)
    {
        var parser = new FluidParser
        {
            Grammar = Parsers.Literals.Text("expected")
                .Then(x => (IReadOnlyList<Statement>)Array.Empty<Statement>())
                .ElseError("Expected text")
        };
#if COMPILED
        parser = parser.Compile();
#endif

        var exception = Assert.Throws<ParseException>(() => parser.Parse(source));

        Assert.Equal(source, exception.TemplateSource);
        Assert.Equal(TextPosition.Start, exception.Position);
        Assert.Equal($"Expected text at (1:1)\nSource:\n{excerpt}", exception.Message);
    }

    [Fact]
    public void CustomParserExceptionGetsMissingMetadata()
    {
        var original = new ParseException("Custom error");
        var parser = new FluidParser
        {
            Grammar = Parsers.Literals.Text("hello")
                .Then(x => ThrowParseException(original))
        };
#if COMPILED
        parser = parser.Compile();
#endif

        var exception = Assert.Throws<ParseException>(() => parser.Parse("hello"));

        Assert.Equal("Custom error", exception.Message);
        Assert.Equal("hello", exception.TemplateSource);
        Assert.Equal(5, exception.Position.Value.Offset);
        Assert.Same(original, exception.InnerException);
    }

    [Fact]
    public void CustomParserExceptionKeepsSuppliedMetadata()
    {
        var original = new ParseException("Custom error", "custom source", new TextPosition(2, 1, 3));
        var parser = new FluidParser
        {
            Grammar = Parsers.Literals.Text("hello")
                .Then(x => ThrowParseException(original))
        };
#if COMPILED
        parser = parser.Compile();
#endif

        var exception = Assert.Throws<ParseException>(() => parser.Parse("hello"));

        Assert.Same(original, exception);
        Assert.Equal("custom source", exception.TemplateSource);
        Assert.Equal(2, exception.Position.Value.Offset);
    }

    private static IReadOnlyList<Statement> ThrowParseException(ParseException exception) => throw exception;
}
