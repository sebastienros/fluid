using Fluid.Parser;
using Parlot;
using System;
using System.Collections.Generic;
using Xunit;

namespace Fluid.Tests;

public class LogTests
{
    private readonly ITestOutputHelper _output;

    public LogTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void LogsParsingEvent()
    {
        var source = """
            {% assign people1 = "alice, bob, carol" | split: ", " %}
            {% for p in people %}{{ p }}{% endfor %} 
        """;

        var dic = new Dictionary<object, int>();

        var parseContext = new FluidParseContext(source);
        var indent = 0;
        parseContext.OnEnterParser = (parser, ctx) =>
        {
            dic.TryAdd(parser, 0);
            dic[parser]++;

            indent += 4;
            var cursor = ctx.Scanner.Cursor;
            _output.WriteLine($"{new string(' ', Math.Max(indent, 0))} {parser} {{ {cursor.Position}: {cursor.Span.Slice(0, Math.Min(cursor.Span.Length, 15)).ToString().Replace(Environment.NewLine, "\\n")}");
        };

        parseContext.OnExitParser = (parser, ctx) =>
        {
            dic.TryAdd(parser, 0);
            dic[parser]--;

            indent -= 4;
            var cursor = ctx.Scanner.Cursor;
            _output.WriteLine($"{new string(' ', Math.Max(indent, 0))} }} {cursor.Position}: {cursor.Span.Slice(0, Math.Min(cursor.Span.Length, 15)).ToString().Replace(Environment.NewLine, "\\n")}");
        };

        var parser = new FluidParser();
        parser.Grammar.Parse(parseContext);

        foreach (var e in dic)
        {
            _output.WriteLine($"{e.Value != 0} {e.Key} {e.Value}");
        }
    }
}
