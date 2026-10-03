using System;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using Fluid.Ast;
using Fluid.Tests.Mocks;
using Fluid.Values;
using Xunit;

namespace Fluid.Tests
{
    public class RenderStatementSyncTests
    {
#if COMPILED
        private static readonly FluidParser _parser = new FluidParser().Compile();
#else
        private static readonly FluidParser _parser = new FluidParser();
#endif

        [Fact]
        public void SynchronousRender_CompletesSynchronouslyAndRestoresScope()
        {
            var nested = new SynchronousTemplate();
            var context = CreateContext(nested);
            var rootScope = context.LocalScope;
            var statement = new RenderStatement(_parser, "snippet");

            var task = statement.WriteToAsync(new TestFluidOutput(), HtmlEncoder.Default, context);

            Assert.True(task.IsCompletedSuccessfully);
            Assert.Equal(Completion.Normal, task.Result);
            Assert.NotNull(nested.ObservedScope);
            Assert.NotSame(rootScope, nested.ObservedScope);
            Assert.Same(rootScope, context.LocalScope);
        }

        [Fact]
        public async Task SuspendedFileLoad_DoesNotEnterScopeEarly()
        {
            var provider = new ControlledFileProvider();
            var context = new TemplateContext(new TemplateOptions { FileProvider = provider });
            var rootScope = context.LocalScope;
            var statement = new RenderStatement(_parser, "snippet");

            var task = statement.WriteToAsync(new TestFluidOutput(), HtmlEncoder.Default, context);

            Assert.False(task.IsCompletedSuccessfully);
            Assert.Same(rootScope, context.LocalScope);

            provider.SetResult("x");

            Assert.Equal(Completion.Normal, await task);
            Assert.Same(rootScope, context.LocalScope);
        }

        [Fact]
        public async Task SuspendedRender_RestoresScopeAfterCompletion()
        {
            var nested = new ControlledTemplate();
            var context = CreateContext(nested);
            var rootScope = context.LocalScope;
            var statement = new RenderStatement(_parser, "snippet");

            var task = statement.WriteToAsync(new TestFluidOutput(), HtmlEncoder.Default, context);

            Assert.False(task.IsCompletedSuccessfully);
            Assert.NotSame(rootScope, context.LocalScope);

            nested.SetResult();

            Assert.Equal(Completion.Normal, await task);
            Assert.Same(rootScope, context.LocalScope);
        }

        [Fact]
        public void SynchronousRenderException_RestoresScope()
        {
            var context = CreateContext(new SynchronousThrowingTemplate());
            var rootScope = context.LocalScope;
            var statement = new RenderStatement(_parser, "snippet");

            var exception = Assert.Throws<InvalidOperationException>(
                () => statement.WriteToAsync(new TestFluidOutput(), HtmlEncoder.Default, context));

            Assert.Equal("render failed", exception.Message);
            Assert.Same(rootScope, context.LocalScope);
        }

        [Fact]
        public async Task SuspendedRenderException_RestoresScope()
        {
            var nested = new ControlledTemplate();
            var context = CreateContext(nested);
            var rootScope = context.LocalScope;
            var statement = new RenderStatement(_parser, "snippet");

            var task = statement.WriteToAsync(new TestFluidOutput(), HtmlEncoder.Default, context);
            nested.SetException(new InvalidOperationException("render failed"));

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => task.AsTask());

            Assert.Equal("render failed", exception.Message);
            Assert.Same(rootScope, context.LocalScope);
        }

        [Fact]
        public async Task SuspendedRenderCancellation_RestoresScope()
        {
            var nested = new ControlledTemplate();
            var context = CreateContext(nested);
            var rootScope = context.LocalScope;
            var statement = new RenderStatement(_parser, "snippet");

            var task = statement.WriteToAsync(new TestFluidOutput(), HtmlEncoder.Default, context);
            nested.SetCanceled();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task.AsTask());
            Assert.Same(rootScope, context.LocalScope);
        }

        [Theory]
        [InlineData("break")]
        [InlineData("continue")]
        public void NestedCompletion_DoesNotEscapeRender(string completion)
        {
            var provider = new MockFileProvider()
                .Add("flow.liquid", $"{{% {completion} %}}");
            var context = new TemplateContext(new TemplateOptions { FileProvider = provider })
                .SetValue("items", new[] { 1, 2 });
            var template = _parser.Parse(
                "{% for item in items %}a{% render 'flow' %}b{% endfor %}");

            Assert.Equal("abab", template.Render(context));
        }

        private static TemplateContext CreateContext(IFluidTemplate nested)
        {
            var provider = new MockFileProvider().Add("snippet.liquid", "");
            return new TemplateContext(new TemplateOptions
            {
                FileProvider = provider,
                TemplateParsed = (_, _) => nested
            });
        }

        private sealed class SynchronousTemplate : IFluidTemplate
        {
            public Scope ObservedScope { get; private set; }

            public ValueTask RenderAsync(
                IFluidOutput output,
                TextEncoder encoder,
                TemplateContext context)
            {
                ObservedScope = context.LocalScope;
                return default;
            }
        }

        private sealed class SynchronousThrowingTemplate : IFluidTemplate
        {
            public ValueTask RenderAsync(
                IFluidOutput output,
                TextEncoder encoder,
                TemplateContext context) =>
                throw new InvalidOperationException("render failed");
        }

        private sealed class ControlledTemplate : IFluidTemplate
        {
            private readonly TaskCompletionSource _completion =
                new(TaskCreationOptions.RunContinuationsAsynchronously);

            public ValueTask RenderAsync(
                IFluidOutput output,
                TextEncoder encoder,
                TemplateContext context) =>
                new(_completion.Task);

            public void SetResult() => _completion.SetResult();

            public void SetException(Exception exception) => _completion.SetException(exception);

            public void SetCanceled() => _completion.SetCanceled();
        }

        private sealed class ControlledFileProvider : ITemplateFileProvider
        {
            private readonly TaskCompletionSource<TemplateSourceInfo> _completion =
                new(TaskCreationOptions.RunContinuationsAsynchronously);

            public ValueTask<TemplateSourceInfo> GetFileInfoAsync(
                string subpath,
                TemplateContext context,
                CancellationToken cancellationToken) =>
                new(_completion.Task);

            public void SetResult(string content)
            {
                var bytes = Encoding.UTF8.GetBytes(content);
                _completion.SetResult(
                    new TemplateSourceInfo(
                        DateTimeOffset.UnixEpoch,
                        _ => new ValueTask<Stream>(new MemoryStream(bytes, writable: false))));
            }
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
