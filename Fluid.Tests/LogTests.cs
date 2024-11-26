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
        parseContext.OnEnterParser = (parser, ctx) =>
        {
            var cursor = ctx.Scanner.Cursor;
            _output.WriteLine($"{parser} {cursor.Position} ...{cursor.Span.Slice(0, Math.Min(cursor.Span.Length, 15))}");
        };

        var parser = new FluidParser();
        parser.Grammar.Parse(parseContext);
    }
}
