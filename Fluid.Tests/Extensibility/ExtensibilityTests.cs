using Fluid.Ast;
using Fluid.Parser;
using System.Threading.Tasks;
using Xunit;

namespace Fluid.Tests.Extensibility
{
    public class ExtensibilityTests
    {
        [Fact]
        public void ShouldRenderEmptyTags()
        {
            var parser = new CustomParser();

            parser.RegisterEmptyTag("hello", (w, e, c) =>
            {
                w.Write("Hello World");

                return Statement.Normal();
            });

            var template = parser.Parse("{% hello %}");
            var result = template.Render();

            Assert.Equal("Hello World", result);
        }

        [Fact]
        public void ShouldReportAndErrorOnEmptyTags()
        {
            var parser = new CustomParser();

            parser.RegisterEmptyTag("hello", (w, e, c) =>
            {
                w.Write("Hello World");

                return Statement.Normal();
            });

            Assert.Throws<ParseException>(() => parser.Parse("{% hello foo %}"));
        }

        [Fact]
        public void ShouldRenderIdentifierTags()
        {
            var parser = new CustomParser();

            parser.RegisterIdentifierTag("hello", (s, w, e, c) =>
            {
                w.Write("Hello ");
                w.Write(s);

                return Statement.Normal();
            });

            var template = parser.Parse("{% hello test %}");
            var result = template.Render();

            Assert.Equal("Hello test", result);
        }

        [Fact]
        public void ShouldRenderEmptyBlocks()
        {
            var parser = new CustomParser();

            parser.RegisterEmptyBlock("hello", static (s, w, e, c) =>
            {
                w.Write("Hello World");
                return s.RenderStatementsAsync(w, e, c);
            });

            var template = parser.Parse("{% hello %} hi {%- endhello %}");
            var result = template.Render();

            Assert.Equal("Hello World hi", result);
        }

        [Fact]
        public void ShouldRenderIdentifierBlocks()
        {
            var parser = new CustomParser();

            parser.RegisterIdentifierBlock("hello", (i, s, w, e, c) =>
            {
                w.Write("Hello ");
                w.Write(i);
                return s.RenderStatementsAsync(w, e, c);
            });

            var template = parser.Parse("{% hello test %} hi {%- endhello %}");
            var result = template.Render();

            Assert.Equal("Hello test hi", result);
        }

        [Fact]
        public void CustomBlockShouldReturnErrorMessage()
        {
            var parser = new CustomParser();

            parser.RegisterEmptyBlock("hello", static (s, w, e, c) =>
            {
                w.Write("Hello World");
                return s.RenderStatementsAsync(w, e, c);
            });

            parser.TryParse("{% hello %} hi {%- endhello %} {% endhello %}", out var template, out var error);

            Assert.Null(template);
            Assert.Contains("Unknown tag 'endhello'", error);
        }

        [Theory]
        [InlineData("{% hello %}")]
        [InlineData("{% if true %}{% hello %}{% endif %}")]
        [InlineData("{% liquid\nhello\n%}")]
        public void ShouldObserveDirectTagDictionaryChanges(string source)
        {
            var parser = new FluidParser(new FluidParserOptions { AllowLiquidTag = true });
            parser.RegisterEmptyTag("first", static (output, encoder, context) =>
            {
                output.Write("first");
                return Statement.Normal();
            });
            parser.RegisterEmptyTag("second", static (output, encoder, context) =>
            {
                output.Write("second");
                return Statement.Normal();
            });

            parser.RegisteredTags["hello"] = parser.RegisteredTags["first"];
            Assert.Equal("first", parser.Parse(source).Render());

            parser.RegisteredTags["hello"] = parser.RegisteredTags["second"];
            Assert.Equal("second", parser.Parse(source).Render());

            Assert.True(parser.RegisteredTags.Remove("hello"));
            parser.RegisteredTags["replacement"] = parser.RegisteredTags["first"];
            Assert.Equal("first", parser.Parse(source.Replace("hello", "replacement")).Render());

            parser.RegisteredTags["replacement"] = parser.RegisteredTags["second"];
            Assert.True(parser.Grammar.TryParse(new FluidParseContext(source.Replace("hello", "replacement")), out var statements, out var error));
            Assert.Null(error);
            Assert.Equal("second", new FluidTemplate(statements).Render());
        }

        [Theory]
        [InlineData("{% hello %}")]
        [InlineData("{% liquid\nhello\n%}")]
        public void ShouldRejectRemovedTags(string source)
        {
            var parser = new FluidParser(new FluidParserOptions { AllowLiquidTag = true });
            parser.RegisteredTags["hello"] = parser.RegisteredTags["break"];
            Assert.NotNull(parser.Parse(source));

            Assert.True(parser.RegisteredTags.Remove("hello"));

            Assert.False(parser.TryParse(source, out var template, out var error));
            Assert.Null(template);
            Assert.Contains("Unknown tag 'hello'", error);
        }

        [Fact]
        public void ShouldParseRegisteredTagsConcurrently()
        {
            var parser = new FluidParser(new FluidParserOptions { AllowLiquidTag = true });
            parser.RegisterEmptyTag("hello", static (output, encoder, context) =>
            {
                output.Write("hello");
                return Statement.Normal();
            });
            const string source = "{% if true %}{% hello %}{% if true %}{% hello %}{% endif %}{% endif %}{% liquid\nhello\n%}{% hello %}";

            Parallel.For(0, 100, _ => Assert.Equal("hellohellohellohello", parser.Parse(source).Render()));
        }

        [Fact]
        public void ShouldAddOperator()
        {
            var parser = new CustomParser();

            parser.RegisteredOperators["xor"] = (a, b) => new XorBinaryExpression(a, b);

            parser.TryParse("{% if true xor false %}true{% endif %}", out var template, out var error);

            Assert.Equal("true", template.Render());
        }
    }
}
