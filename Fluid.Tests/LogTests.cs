using Fluid.Parser;
using Parlot;
using System;
using Xunit;
using Xunit.Abstractions;

namespace Fluid.Tests;

public class LogTests
{
    private readonly ITestOutputHelper _output;

    public LogTests(ITestOutputHelper output)
    {
        _output = output;
        _output = output;
    }

    [Fact]
    public void LogsParsingEvent()
    {
        var source = """
            {% assign people1 = "alice, bob, carol" | split: ", " %}
            {% for p in people %}{{ p }}{% endfor %} 
        """;

        var parseContext = new FluidParseContext(source);
        var indent = 0;
        parseContext.OnEnterParser = (parser, ctx) =>
        {
            indent += 4;
            var cursor = ctx.Scanner.Cursor;
            _output.WriteLine($"{new string(' ', indent)} {parser} {cursor.Position} ...{cursor.Span.Slice(0, Math.Min(cursor.Span.Length, 15))}");
        };
        parseContext.OnExitParser = (parser, ctx) => indent -= 4;

        var parser = new FluidParser();
        parser.Grammar.Parse(parseContext);
    }
}
