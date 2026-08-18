using System;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Fluid.Tests.Mocks;
using Fluid.Values;
using Xunit;

namespace Fluid.Tests
{
    public class RenderStatementAllocationTests
    {
#if COMPILED
        private static readonly FluidParser _parser = new FluidParser().Compile();
#else
        private static readonly FluidParser _parser = new FluidParser();
#endif

        [Fact]
        public void RenderWithoutArguments_SeesOnlyRootAndOptionScopes()
        {
            var provider = new MockFileProvider()
                .Add("scope.liquid", "{{ option }}|{{ root }}|{{ local }}");
            var options = new TemplateOptions { FileProvider = provider };
            options.Scope.SetValue("option", new StringValue("option"));
            var context = new TemplateContext(options).SetValue("root", "root");
            var template = _parser.Parse("{% assign local = 'local' %}{% render 'scope' %}");

            Assert.Equal("option|root|", template.Render(context));
        }

        [Fact]
        public void RenderWithAliasAndArguments_PublishesArgumentsAfterEvaluation()
        {
            var provider = new MockFileProvider()
                .Add("arguments.liquid", "{{ item }}|{{ first }}|{{ second }}");
            var context = new TemplateContext(new TemplateOptions { FileProvider = provider })
                .SetValue("value", "value")
                .SetValue("first", "outer");
            var template = _parser.Parse(
                "{% render 'arguments' with value as item, first: 'inner', second: first %}");

            Assert.Equal("value|inner|outer", template.Render(context));
            Assert.Equal("outer", context.GetValue("first").ToStringValue());
        }

        [Fact]
        public void RenderFor_PreservesRenderLoopAcrossNestedLoops()
        {
            var provider = new MockFileProvider()
                .Add(
                    "item.liquid",
                    "{{ forloop.index }}[" +
                    "{% for inner in inners %}{{ forloop.parentloop.index }}.{{ forloop.index }}{% endfor %}" +
                    "]{{ forloop.index }};");
            var context = new TemplateContext(new TemplateOptions { FileProvider = provider })
                .SetValue("items", new[] { "a", "b" })
                .SetValue("inners", new[] { 1, 2 });
            var template = _parser.Parse("{% render 'item' for items as item %}");

            Assert.Equal("1[.1.2]1;2[.1.2]2;", template.Render(context));
        }

        [Fact]
        public void NestedRender_PassesOnlyExplicitValues()
        {
            var provider = new MockFileProvider()
                .Add("outer.liquid", "{% assign hidden = 'hidden' %}{% render 'inner' with item as item %}")
                .Add("inner.liquid", "{{ item }}|{{ hidden }}|{{ root }}");
            var context = new TemplateContext(new TemplateOptions { FileProvider = provider })
                .SetValue("value", "value")
                .SetValue("root", "root");
            var template = _parser.Parse("{% render 'outer' with value as item %}");

            Assert.Equal("value||root", template.Render(context));
        }

        [Fact]
        public async Task RenderArguments_InvokeAssignedBeforePublicationAndRestoreScopeOnError()
        {
            var provider = new MockFileProvider()
                .Add("arguments.liquid", "{{ first }}{{ second }}");
            var options = new TemplateOptions { FileProvider = provider };
            var callbackCount = 0;
            options.Assigned = (identifier, value, context) =>
            {
                callbackCount++;
                Assert.IsType<UndefinedValue>(context.GetValue("first"));

                if (identifier == "second")
                {
                    throw new InvalidOperationException("assigned failed");
                }

                return new ValueTask<FluidValue>(value);
            };

            var context = new TemplateContext(options);
            var rootScope = context.LocalScope;
            var template = _parser.Parse("{% render 'arguments', first: 'a', second: 'b' %}");

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => template.RenderAsync(new TestFluidOutput(), HtmlEncoder.Default, context).AsTask());

            Assert.Equal("assigned failed", exception.Message);
            Assert.Equal(2, callbackCount);
            Assert.Same(rootScope, context.LocalScope);
            Assert.IsType<UndefinedValue>(context.GetValue("first"));

            context.Assigned = null;
            await template.RenderAsync(new TestFluidOutput(), HtmlEncoder.Default, context);
            Assert.Same(rootScope, context.LocalScope);
        }

        [Fact]
        public void RenderScopesAndArguments_StayWithinAllocationBudget()
        {
            var provider = new MockFileProvider()
                .Add("empty.liquid", "x")
                .Add("arguments.liquid", "{{ first }}{{ second }}");
            var context = new TemplateContext(new TemplateOptions { FileProvider = provider })
                .SetValue("value", "v");
            var noArguments = _parser.Parse("{% render 'empty' %}");
            var arguments = _parser.Parse("{% render 'arguments', first: value, second: value %}");
            var output = new TestFluidOutput();

            Render(noArguments, output, context, 10);
            Render(arguments, output, context, 10);

            var noArgumentBytes = Measure(noArguments, output, context);
            var argumentBytes = Measure(arguments, output, context);

            Assert.InRange(noArgumentBytes, 1, 1_100);
            Assert.InRange(argumentBytes, 1, 1_200);
        }

        private static long Measure(IFluidTemplate template, TestFluidOutput output, TemplateContext context)
        {
            const int Iterations = 1_000;

            var before = GC.GetAllocatedBytesForCurrentThread();
            Render(template, output, context, Iterations);
            return (GC.GetAllocatedBytesForCurrentThread() - before) / Iterations;
        }

        private static void Render(
            IFluidTemplate template,
            TestFluidOutput output,
            TemplateContext context,
            int iterations)
        {
            for (var i = 0; i < iterations; i++)
            {
                output.Reset();
                var task = template.RenderAsync(output, NullEncoder.Default, context);
                Assert.True(task.IsCompletedSuccessfully);
                task.GetAwaiter().GetResult();
            }
        }

        private sealed class TestFluidOutput : IFluidOutput
        {
            private readonly char[] _buffer = new char[64];
            private int _written;

            public void Advance(int count) => _written += count;

            public Memory<char> GetMemory(int sizeHint = 0) => _buffer.AsMemory(_written);

            public Span<char> GetSpan(int sizeHint = 0) => _buffer.AsSpan(_written);

            public void Write(string value)
            {
                value.CopyTo(0, _buffer, _written, value.Length);
                _written += value.Length;
            }

            public void Write(char[] buffer, int index, int count)
            {
                buffer.AsSpan(index, count).CopyTo(_buffer.AsSpan(_written));
                _written += count;
            }

            public ValueTask FlushAsync() => default;

            public void Reset() => _written = 0;
        }
    }
}
