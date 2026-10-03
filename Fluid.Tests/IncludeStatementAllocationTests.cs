using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using Fluid.Ast;
using Fluid.Tests.Mocks;
using Fluid.Values;
using Xunit;

namespace Fluid.Tests
{
    public class IncludeStatementAllocationTests
    {
#if COMPILED
        private static readonly FluidParser _parser = new FluidParser().Compile();
#else
        private static readonly FluidParser _parser = new FluidParser();
#endif

        [Fact]
        public void IncludeArguments_AreRemovedWhileAssignmentsPersist()
        {
            var provider = new MockFileProvider()
                .Add("snippet.liquid", "{{ argument }}{% assign persisted = argument %}");
            var context = new TemplateContext(new TemplateOptions { FileProvider = provider });
            var template = _parser.Parse(
                "{% include 'snippet', argument: 'inner' %}|{{ argument }}|{{ persisted }}");

            Assert.Equal("inner||inner", template.Render(context));
            Assert.IsType<UndefinedValue>(context.GetValue("argument"));
        }

        [Fact]
        public void IncludeDuplicateArguments_PreserveEvaluationAndCleanupOrder()
        {
            var provider = new MockFileProvider()
                .Add("snippet.liquid", "{{ value }}");
            var context = new TemplateContext(new TemplateOptions { FileProvider = provider });
            var template = _parser.Parse(
                "{% include 'snippet', value: 'first', value: value %}|{{ value }}");

            Assert.Equal("first|", template.Render(context));
            Assert.IsType<UndefinedValue>(context.GetValue("value"));
        }

        [Fact]
        public void NestedIncludeArguments_RestoreTheOuterIncludeValue()
        {
            var provider = new MockFileProvider()
                .Add("outer.liquid", "{{ value }}|{% include 'inner', value: 'inner' %}|{{ value }}")
                .Add("inner.liquid", "{{ value }}");
            var context = new TemplateContext(new TemplateOptions { FileProvider = provider });
            var template = _parser.Parse(
                "{% include 'outer', value: 'outer' %}|{{ value }}");

            Assert.Equal("outer|inner|outer|", template.Render(context));
            Assert.IsType<UndefinedValue>(context.GetValue("value"));
        }

        [Fact]
        public async Task IncludeBindingsAndArguments_RestoreExistingValues()
        {
            var provider = new MockFileProvider()
                .Add("snippet.liquid", "{{ item }}|{{ value }}");
            var context = new TemplateContext(new TemplateOptions { FileProvider = provider })
                .SetValue("item", "outer-item")
                .SetValue("value", "outer-value");
            var include = new IncludeStatement(
                _parser,
                new LiteralExpression(new StringValue("snippet")),
                with: new LiteralExpression(new StringValue("inner-item")),
                alias: "item",
                assignStatements: new List<AssignStatement>
                {
                    new("value", new LiteralExpression(new StringValue("inner-value")))
                });
            var writer = new StringWriter();

            await include.WriteToAsync(writer, NullEncoder.Default, context);

            Assert.Equal("inner-item|inner-value", writer.ToString());
            Assert.Equal("outer-item", context.GetValue("item").ToStringValue());
            Assert.Equal("outer-value", context.GetValue("value").ToStringValue());
        }

        [Theory]
        [InlineData("break", "before|")]
        [InlineData("continue", "beforebefore|")]
        public void IncludeArguments_AreRemovedOnLoopCompletion(string completion, string expected)
        {
            var provider = new MockFileProvider()
                .Add("flow.liquid", $"{{% {completion} %}}");
            var context = new TemplateContext(new TemplateOptions { FileProvider = provider })
                .SetValue("items", new[] { 1, 2 });
            var template = _parser.Parse(
                "{% for item in items %}before{% include 'flow', argument: 'inner' %}after{% endfor %}|{{ argument }}");

            Assert.Equal(expected, template.Render(context));
            Assert.IsType<UndefinedValue>(context.GetValue("argument"));
        }

        [Fact]
        public async Task IncludeArguments_AreRemovedWhenFallbackTemplateThrows()
        {
            var provider = new MockFileProvider()
                .Add("snippet.liquid", "");
            var options = new TemplateOptions
            {
                FileProvider = provider,
                TemplateParsed = (_, _) => new ThrowingTemplate()
            };
            var context = new TemplateContext(options)
                .SetValue("existing", "outer");
            var rootScope = context.LocalScope;
            var template = _parser.Parse(
                "{% include 'snippet', argument: 'inner', existing: 'inner' %}");

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => template.RenderAsync(new TestFluidOutput(), HtmlEncoder.Default, context).AsTask());

            Assert.Equal("render failed", exception.Message);
            Assert.Same(rootScope, context.LocalScope);
            Assert.IsType<UndefinedValue>(context.GetValue("argument"));
            Assert.Equal("outer", context.GetValue("existing").ToStringValue());
        }

        [Fact]
        public async Task IncludeArguments_AreRemovedWhenArgumentEvaluationThrows()
        {
            var provider = new MockFileProvider()
                .Add("snippet.liquid", "");
            var context = new TemplateContext(new TemplateOptions { FileProvider = provider })
                .SetValue("existing", "outer");
            var rootScope = context.LocalScope;
            var include = new IncludeStatement(
                _parser,
                new LiteralExpression(new StringValue("snippet")),
                assignStatements: new List<AssignStatement>
                {
                    new("argument", new LiteralExpression(new StringValue("inner"))),
                    new("existing", new ThrowingExpression())
                });

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => include.WriteToAsync(new TestFluidOutput(), HtmlEncoder.Default, context).AsTask());

            Assert.Equal("evaluation failed", exception.Message);
            Assert.Same(rootScope, context.LocalScope);
            Assert.IsType<UndefinedValue>(context.GetValue("argument"));
            Assert.Equal("outer", context.GetValue("existing").ToStringValue());
        }

        [Fact]
        public async Task IncludeArguments_AreRemovedWhenFallbackTemplateIsCanceled()
        {
            var provider = new MockFileProvider()
                .Add("snippet.liquid", "");
            var options = new TemplateOptions
            {
                FileProvider = provider,
                TemplateParsed = (_, _) => new CanceledTemplate()
            };
            var context = new TemplateContext(options);
            var rootScope = context.LocalScope;
            var template = _parser.Parse(
                "{% include 'snippet', argument: 'inner' %}");

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => template.RenderAsync(new TestFluidOutput(), HtmlEncoder.Default, context).AsTask());

            Assert.Same(rootScope, context.LocalScope);
            Assert.IsType<UndefinedValue>(context.GetValue("argument"));
        }

        private sealed class ThrowingTemplate : IFluidTemplate
        {
            public ValueTask RenderAsync(IFluidOutput output, TextEncoder encoder, TemplateContext context) =>
                new(Task.FromException(new InvalidOperationException("render failed")));
        }

        private sealed class CanceledTemplate : IFluidTemplate
        {
            public ValueTask RenderAsync(IFluidOutput output, TextEncoder encoder, TemplateContext context) =>
                new(Task.FromCanceled(new CancellationToken(canceled: true)));
        }

        private sealed class ThrowingExpression : Expression
        {
            public override ValueTask<FluidValue> EvaluateAsync(TemplateContext context) =>
                new(Task.FromException<FluidValue>(new InvalidOperationException("evaluation failed")));
        }

        private sealed class TestFluidOutput : IFluidOutput
        {
            public void Advance(int count)
            {
            }

            public Memory<char> GetMemory(int sizeHint = 0) => Memory<char>.Empty;

            public Span<char> GetSpan(int sizeHint = 0) => Span<char>.Empty;

            public void Write(string value)
            {
            }

            public void Write(char[] buffer, int index, int count)
            {
            }

            public ValueTask FlushAsync() => default;
        }
    }
}
