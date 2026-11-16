using System;
using System.Linq;
using System.Threading;
using Fluid.Tests.Mocks;
using Xunit;

namespace Fluid.Tests;

// Deeply nested templates must fail with an error instead of crashing the process with a stack overflow.
public class RecursionDepthTests
{
    private const int StackSize = 1024 * 1024;

    private static readonly FluidParser _parser = new FluidParser(new FluidParserOptions { AllowParentheses = true, AllowLiquidTag = true });

    private static readonly FluidParser _unlimitedParser = new FluidParser(new FluidParserOptions { AllowParentheses = true, AllowLiquidTag = true, MaxRecursionDepth = 0 });

    private static string Repeat(string value, int count) => string.Concat(Enumerable.Repeat(value, count));

    public static TheoryData<string> Templates() =>
    [
        "if",
        "for",
        "capture",
        "case",
        "custom",
        "liquid",
        "parentheses",
        "indexer",
        "filters",
        "and",
    ];

    private static string Nested(string kind, int depth) => kind switch
    {
        "if" => Repeat("{% if true %}", depth) + "x" + Repeat("{% endif %}", depth),
        "for" => Repeat("{% for i in (1..1) %}", depth) + "x" + Repeat("{% endfor %}", depth),
        "capture" => Repeat("{% capture c %}", depth) + "x" + Repeat("{% endcapture %}", depth),
        "case" => Repeat("{% case 1 %}{% when 1 %}", depth) + "x" + Repeat("{% endcase %}", depth),
        "custom" => Repeat("{% block %}", depth) + "x" + Repeat("{% endblock %}", depth),
        "liquid" => "{% liquid\n" + Repeat("if true\n", depth) + "echo 'x'\n" + Repeat("endif\n", depth) + "%}",
        "parentheses" => "{{ " + new string('(', depth) + "'x'" + new string(')', depth) + " }}",
        "indexer" => "{{ a" + Repeat("[a", depth) + Repeat("]", depth) + " }}",
        "filters" => "{{ 'x'" + Repeat(" | downcase", depth) + " }}",
        "and" => "{% if true" + Repeat(" and true", depth) + " %}x{% endif %}",
        "or" => "{% if false" + Repeat(" or false", depth) + " %}x{% endif %}",
        _ => throw new ArgumentException(kind)
    };

    // Replaces EXPR in a template with a group nested depth times.
    private static string NestedGroup(string template, int depth) => template.Replace("EXPR", new string('(', depth) + "1" + new string(')', depth));

    private static FluidParser CreateParser(FluidParser parser)
    {
        parser.RegisterEmptyBlock("block", static (statements, writer, encoder, context) => statements.RenderStatementsAsync(writer, encoder, context));
        return parser;
    }

    private static void RunWithStack(Action action, int stackSize = StackSize)
    {
        Exception exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                exception = e;
            }
        }, stackSize);

        thread.Start();
        thread.Join();

        if (exception != null)
        {
            throw exception;
        }
    }

    [Theory]
    [MemberData(nameof(Templates))]
    public void ShouldParseAndRenderTemplatesWithinTheDefaultDepth(string kind)
    {
        var parser = CreateParser(new FluidParser(new FluidParserOptions { AllowParentheses = true, AllowLiquidTag = true }));
        var source = Nested(kind, 90);

        RunWithStack(() =>
        {
            Assert.True(parser.TryParse(source, out var template, out var error), error);
            var context = new TemplateContext();
            context.SetValue("a", new[] { 0 });
            template.Render(context);
        });
    }

    // Without AllowParentheses a group is still parsed before it's rejected.
    [Theory]
    [InlineData("{{ EXPR }}")]
    [InlineData("{{ 1 | plus: EXPR }}")]
    [InlineData("{% if EXPR %}x{% endif %}")]
    [InlineData("{% assign x = EXPR %}")]
    public void ShouldNotParseDisallowedParenthesesNestedTooDeeply(string template)
    {
        var parser = new FluidParser();

        RunWithStack(() =>
        {
            Assert.False(parser.TryParse(NestedGroup(template, 20), out _, out var error));
            Assert.Contains("Parentheses are not allowed", error);

            Assert.False(parser.TryParse(NestedGroup(template, 2000), out _, out error));
            Assert.Contains("The maximum parser recursion depth of 200 was exceeded", error);
        });
    }

    [Theory]
    [InlineData(0, StackSize)]
    [InlineData(FluidParserOptions.DefaultMaxRecursionDepth, 256 * 1024)]
    public void ShouldNotOverflowTheStackWithDisallowedParentheses(int maxRecursionDepth, int stackSize)
    {
        var parser = new FluidParser(new FluidParserOptions { MaxRecursionDepth = maxRecursionDepth });
        var source = NestedGroup("{{ EXPR }}", 20_000);

        RunWithStack(() => Assert.False(parser.TryParse(source, out _, out _)), stackSize);
    }

    [Theory]
    [MemberData(nameof(Templates))]
    public void ShouldNotParseTemplatesNestedTooDeeply(string kind)
    {
        var parser = CreateParser(new FluidParser(new FluidParserOptions { AllowParentheses = true, AllowLiquidTag = true }));
        var source = Nested(kind, 2000);

        RunWithStack(() =>
        {
            Assert.False(parser.TryParse(source, out _, out var error));
            Assert.Contains("The maximum parser recursion depth of 200 was exceeded", error);
        });
    }

    [Fact]
    public void ShouldUseConfiguredMaxRecursionDepth()
    {
        var parser = new FluidParser(new FluidParserOptions { MaxRecursionDepth = 10 });

        Assert.True(parser.TryParse(Nested("if", 4), out _, out var error), error);
        Assert.False(parser.TryParse(Nested("if", 6), out _, out error));
        Assert.Contains("The maximum parser recursion depth of 10 was exceeded", error);

        Assert.True(parser.TryParse(Nested("filters", 10), out _, out error), error);
        Assert.False(parser.TryParse(Nested("filters", 11), out _, out _));
    }

    [Fact]
    public void ShouldRejectNegativeMaxRecursionDepth()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FluidParserOptions { MaxRecursionDepth = -1 });
    }

    [Theory]
    [InlineData("if")]
    [InlineData("for")]
    [InlineData("capture")]
    [InlineData("liquid")]
    [InlineData("parentheses")]
    [InlineData("indexer")]
    public void ShouldNotOverflowTheStackWhenParsingWithoutLimit(string kind)
    {
        var source = Nested(kind, 20_000);

        RunWithStack(() =>
        {
            Assert.False(_unlimitedParser.TryParse(source, out _, out var error));

            // Fluid's stack checks or Parlot's own, whichever runs out of stack first.
            Assert.Contains("nested too deeply to be parsed", error);
        });
    }

    [Theory]
    [InlineData("filters")]
    [InlineData("and")]
    [InlineData("or")]
    public void ShouldNotOverflowTheStackWhenRenderingWithoutLimit(string kind)
    {
        Assert.True(_unlimitedParser.TryParse(Nested(kind, 20_000), out var template, out var error), error);

        RunWithStack(() => Assert.Throws<InsufficientExecutionStackException>(() => template.Render(new TemplateContext())));
    }

    [Fact]
    public void ShouldNotOverflowTheStackWithRecursiveIncludes()
    {
        var fileProvider = new MockFileProvider();
        fileProvider.Add("a.liquid", Nested("if", 90).Replace("x", "{% include 'a' %}"));

        var options = new TemplateOptionsBuilder().WithFileProvider(fileProvider).WithMaxRecursion(0).Build();
        Assert.True(_parser.TryParse("{% include 'a' %}", out var template, out var error), error);

        RunWithStack(() => Assert.Throws<InsufficientExecutionStackException>(() => template.Render(new TemplateContext(options))));
    }
}
