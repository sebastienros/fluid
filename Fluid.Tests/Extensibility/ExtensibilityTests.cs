using Fluid.Ast;
using Parlot.Fluent;
using Xunit;

namespace Fluid.Tests.Extensibility
{
    public class ExtensibilityTests
    {
        [Fact]
        public void ShouldRenderEmptyTags()
        {
            var parser = new CustomParser();

            parser.RegisterEmptyTag("hello", (s, w, e, c) =>
            {
                w.Write("Hello World");

                return Statement.Normal;
            });

            var template = parser.Parse("{% hello %}");
            var result = template.Render();

            Assert.Equal("Hello World", result);
        }

        [Fact]
        public void ShouldRenderIdentifierTags()
        {
            var parser = new CustomParser();

            parser.RegisterIdentifierTag("hello", (s, w, e, c) =>
            {
                w.Write("Hello ");
                w.Write(s.Identifier);

                return Statement.Normal;
            });

            var template = parser.Parse("{% hello test %}");
            var result = template.Render();

            Assert.Equal("Hello test", result);
        }

        [Fact]
        public void ShouldRenderEmptyBlocks()
        {
            var parser = new CustomParser();

            parser.RegisterEmptyBlock("hello", (s, w, e, c) =>
            {
                w.Write("Hello World");
                return s.RenderBlockAsync(w, e, c);
            });

            var template = parser.Parse("{% hello %} hi {%- endhello %}");
            var result = template.Render();

            Assert.Equal("Hello World hi", result);
        }

        [Fact]
        public void ShouldRenderIdentifierBlocks()
        {
            var parser = new CustomParser();

            parser.RegisterIdentifierBlock("hello", (s, w, e, c) =>
            {
                w.Write("Hello ");
                w.Write(s.Value.ToString());
                return s.RenderBlockAsync(w, e, c);
            });

            var template = parser.Parse("{% hello test %} hi {%- endhello %}");
            var result = template.Render();

            Assert.Equal("Hello test hi", result);
        }
    }
}
