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
    private static FluidParser _parser = new FluidParser();

    [Theory]
    [InlineData("{% assign a 'b' %}", ErrorMessages.EqualAfterAssignIdentifier)]
    [InlineData("{% assign 'foo' %}", ErrorMessages.IdentifierAfterAssign)]
    [InlineData("{% assign %}", ErrorMessages.IdentifierAfterAssign)]
    [InlineData("{% assign a = | filter %}", ErrorMessages.LogicalExpressionStartsFilter)]
    [InlineData("{% assign a = | upcase %}", ErrorMessages.LogicalExpressionStartsFilter)]
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
    [InlineData("hello\r{% assign a 'b' %}", 17, 1, 18)]
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

        var exception = Assert.Throws<ParseException>(() => parser.Parse(source));

        Assert.Equal(source, exception.TemplateSource);
        Assert.Equal(TextPosition.Start, exception.Position);
        Assert.Equal($"Expected text at (1:1)\nSource:\n{excerpt}", exception.Message);
    }

    [Theory]
    [InlineData("", 1, 1, "{% custom invalid %}")]
    [InlineData("prefix", 1, 7, "prefix{% custom invalid %}")]
    [InlineData("prefix\n", 2, 1, "{% custom invalid %}")]
    [InlineData("prefix\r\n", 2, 1, "{% custom invalid %}")]
    public void UnconsumedCustomTagReportsPosition(string prefix, int line, int column, string excerpt)
    {
        var parser = new FluidParser();
        parser.RegisteredTags["custom"] = Parsers.Terms.Text("valid")
            .AndSkip(Parsers.Terms.Text("%}"))
            .Then<Statement>(_ => new NoOpStatement());
        var source = prefix + "{% custom invalid %}\nnext line";

        var context = new FluidParseContext(source);
        Assert.False(parser.Grammar.TryParse(context, out var statements, out var parlotError));
        Assert.Null(statements);
        Assert.NotNull(parlotError);
        Assert.Equal("Unexpected trailing input", parlotError.Message);
        Assert.Equal(prefix.Length, parlotError.Position.Offset);
        Assert.Equal(line, parlotError.Position.Line);
        Assert.Equal(column, parlotError.Position.Column);

        var exception = Assert.Throws<ParseException>(() => parser.Parse(source));
        Assert.Equal(source, exception.TemplateSource);
        Assert.Equal(parlotError.Position, exception.Position);
        Assert.Equal($"Unexpected trailing input at ({line}:{column})\nSource:\n{excerpt}", exception.Message);
        Assert.False(parser.TryParse(source, out var template, out var error));
        Assert.Null(template);
        Assert.Equal(exception.Message, error);
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

        var exception = Assert.Throws<ParseException>(() => parser.Parse("hello"));

        Assert.Same(original, exception);
        Assert.Equal("custom source", exception.TemplateSource);
        Assert.Equal(2, exception.Position.Value.Offset);
    }

    private static IReadOnlyList<Statement> ThrowParseException(ParseException exception) => throw exception;
}
