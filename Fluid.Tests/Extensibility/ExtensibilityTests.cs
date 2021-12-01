using Fluid.Ast;
using Parlot.Fluent;
using System;
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
