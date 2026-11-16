using System;
using System.Linq;
using System.Threading;
using Fluid.Tests.Mocks;
using Xunit;

namespace Fluid.Tests
{
    // Deeply nested templates must fail with an error instead of crashing the process with a stack overflow.
    public class RecursionDepthTests
    {
        private const int StackSize = 1024 * 1024;

        private static string Repeat(string value, int count) => string.Concat(Enumerable.Repeat(value, count));

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
            _ => throw new ArgumentException(kind)
        };

        // Replaces EXPR in a template with a group nested depth times.
        private static string NestedGroup(string template, int depth) => template.Replace("EXPR", new string('(', depth) + "1" + new string(')', depth));

        private static FluidParser CreateParser(bool compiled, int maxRecursionDepth = FluidParserOptions.DefaultMaxRecursionDepth, bool allowParentheses = true)
        {
            var parser = new FluidParser(new FluidParserOptions { AllowParentheses = allowParentheses, AllowLiquidTag = true, MaxRecursionDepth = maxRecursionDepth });
            parser.RegisterEmptyBlock("block", static (statements, writer, encoder, context) => statements.RenderStatementsAsync(writer, encoder, context));
            return compiled ? parser.Compile() : parser;
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
        [InlineData("if", false)]
        [InlineData("for", false)]
        [InlineData("capture", false)]
        [InlineData("case", false)]
        [InlineData("custom", false)]
        [InlineData("liquid", false)]
        [InlineData("parentheses", false)]
        [InlineData("indexer", false)]
        [InlineData("filters", false)]
        [InlineData("and", false)]
        [InlineData("if", true)]
        [InlineData("parentheses", true)]
        [InlineData("indexer", true)]
        [InlineData("filters", true)]
        public void ShouldParseAndRenderTemplatesWithinTheDefaultDepth(string kind, bool compiled)
        {
            var parser = CreateParser(compiled);
            var source = Nested(kind, 90);

            RunWithStack(() =>
            {
                Assert.True(parser.TryParse(source, out var template, out var error), error);
                var context = new TemplateContext();
                context.SetValue("a", new[] { 0 });
                template.Render(context);
            });
        }

        [Theory]
        [InlineData("if", false)]
        [InlineData("for", false)]
        [InlineData("capture", false)]
        [InlineData("case", false)]
        [InlineData("custom", false)]
        [InlineData("liquid", false)]
        [InlineData("parentheses", false)]
        [InlineData("indexer", false)]
        [InlineData("filters", false)]
        [InlineData("and", false)]
        [InlineData("if", true)]
        [InlineData("parentheses", true)]
        [InlineData("indexer", true)]
        [InlineData("filters", true)]
        public void ShouldNotParseTemplatesNestedTooDeeply(string kind, bool compiled)
        {
            var parser = CreateParser(compiled);
            var source = Nested(kind, 2000);

            RunWithStack(() =>
            {
                Assert.False(parser.TryParse(source, out _, out var error));
                Assert.Contains("The maximum parser recursion depth of 100 was exceeded", error);
            });
        }

        // Without AllowParentheses a group is still parsed before it's rejected.
        [Theory]
        [InlineData("{{ EXPR }}", false)]
        [InlineData("{{ 1 | plus: EXPR }}", false)]
        [InlineData("{% if EXPR %}x{% endif %}", false)]
        [InlineData("{% assign x = EXPR %}", false)]
        [InlineData("{{ EXPR }}", true)]
        [InlineData("{% if EXPR %}x{% endif %}", true)]
        public void ShouldNotParseDisallowedParenthesesNestedTooDeeply(string template, bool compiled)
        {
            var parser = CreateParser(compiled, allowParentheses: false);

            RunWithStack(() =>
            {
                Assert.False(parser.TryParse(NestedGroup(template, 20), out _, out var error));
                Assert.Contains("Parentheses are not allowed", error);

                Assert.False(parser.TryParse(NestedGroup(template, 2000), out _, out error));
                Assert.Contains("The maximum parser recursion depth of 100 was exceeded", error);
            });
        }

        [Theory]
        [InlineData(0, StackSize, false)]
        [InlineData(0, StackSize, true)]
        [InlineData(FluidParserOptions.DefaultMaxRecursionDepth, 256 * 1024, false)]
        [InlineData(FluidParserOptions.DefaultMaxRecursionDepth, 256 * 1024, true)]
        public void ShouldNotOverflowTheStackWithDisallowedParentheses(int maxRecursionDepth, int stackSize, bool compiled)
        {
            var parser = CreateParser(compiled, maxRecursionDepth, allowParentheses: false);
            var source = NestedGroup("{{ EXPR }}", 20_000);

            RunWithStack(() => Assert.False(parser.TryParse(source, out _, out _)), stackSize);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ShouldUseConfiguredMaxRecursionDepth(bool compiled)
        {
            var parser = CreateParser(compiled, 10);

            Assert.True(parser.TryParse(Nested("if", 10), out _, out var error), error);
            Assert.False(parser.TryParse(Nested("if", 11), out _, out error));
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
        [InlineData("if", false)]
        [InlineData("for", false)]
        [InlineData("capture", false)]
        [InlineData("liquid", false)]
        [InlineData("parentheses", false)]
        [InlineData("indexer", false)]
        [InlineData("if", true)]
        [InlineData("parentheses", true)]
        public void ShouldNotOverflowTheStackWhenParsingWithoutLimit(string kind, bool compiled)
        {
            var parser = CreateParser(compiled, 0);
            var source = Nested(kind, 20_000);

            RunWithStack(() =>
            {
                Assert.False(parser.TryParse(source, out _, out var error));
                Assert.Contains("The template is nested too deeply to be parsed", error);
            });
        }

        [Theory]
        [InlineData("filters")]
        [InlineData("and")]
        public void ShouldNotOverflowTheStackWhenRenderingWithoutLimit(string kind)
        {
            Assert.True(CreateParser(false, 0).TryParse(Nested(kind, 20_000), out var template, out var error), error);

            RunWithStack(() => Assert.Throws<InsufficientExecutionStackException>(() => template.Render(new TemplateContext())));
        }

        [Fact]
        public void ShouldNotOverflowTheStackWithRecursiveIncludes()
        {
            var fileProvider = new MockFileProvider();
            fileProvider.Add("a.liquid", Nested("if", 90).Replace("x", "{% include 'a' %}"));

            var options = new TemplateOptions { FileProvider = fileProvider, MaxRecursion = 0 };
            Assert.True(CreateParser(false).TryParse("{% include 'a' %}", out var template, out var error), error);

            RunWithStack(() => Assert.Throws<InsufficientExecutionStackException>(() => template.Render(new TemplateContext(options))));
        }
    }
}
