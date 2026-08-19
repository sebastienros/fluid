using System;
using System.Linq;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using Fluid.Ast;
using Fluid.Parser;
using Xunit;

namespace Fluid.Tests
{
    public class BufferFluidOutputTests
    {
        [Fact]
        public void EmptyOutputReturnsEmptyString()
        {
            using var output = CreateOutput(1);

            Assert.Equal(string.Empty, output.ToString());
        }

        [Fact]
        public void SupportsSpanMemoryAndAdvanceAcrossGrowthBoundaries()
        {
            using var output = CreateOutput(4);

            output.Write("abc");
            var span = output.GetSpan(2);
            "de".AsSpan().CopyTo(span);
            output.Advance(2);

            var memory = output.GetMemory(5);
            "fghij".AsMemory().CopyTo(memory);
            output.Advance(5);

            Assert.Equal("abcdefghij", output.ToString());
        }

        [Fact]
        public void SupportsMixedStringArrayAndBufferWriterWrites()
        {
            using var output = CreateOutput(3);
            var chars = "23456".ToCharArray();

            output.Write("01");
            output.Write(chars, 0, chars.Length);
            var span = output.GetSpan();
            span[0] = '7';
            output.Advance(1);
            var memory = output.GetMemory(2);
            memory.Span[0] = '8';
            memory.Span[1] = '9';
            output.Advance(2);

            Assert.Equal("0123456789", output.ToString());
        }

        [Theory]
        [InlineData(255)]
        [InlineData(256)]
        [InlineData(257)]
        [InlineData(32 * 1024 - 1)]
        [InlineData(32 * 1024)]
        [InlineData(32 * 1024 + 1)]
        public void PreservesContentAtSegmentBoundaries(int length)
        {
            using var output = CreateOutput(256);
            var expected = CreatePattern(length);

            for (var offset = 0; offset < expected.Length; offset += 113)
            {
                output.Write(expected, offset, Math.Min(113, expected.Length - offset));
            }

            Assert.Equal(new string(expected), output.ToString());
        }

        [Fact]
        public void RejectsInvalidBufferWriterOperations()
        {
            using var output = CreateOutput(4);

            Assert.Throws<ArgumentOutOfRangeException>(() => output.GetSpan(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => output.GetMemory(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => output.Advance(-1));
            var available = output.GetSpan().Length;
            Assert.Throws<ArgumentOutOfRangeException>(() => output.Advance(available + 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => output.Write(['a'], 1, 1));
        }

        [Fact]
        public void DisposesEverySegmentAfterFailure()
        {
            Assert.Throws<OperationCanceledException>((Action)(() =>
            {
                using var output = CreateOutput(8);
                output.Write(new string('x', 100_000));
                output.GetSpan(100_000);
                throw new OperationCanceledException();
            }));

            using var subsequent = CreateOutput(8);
            subsequent.Write("still usable");
            Assert.Equal("still usable", subsequent.ToString());
        }

        [Fact]
        public async Task RendersLargeAndHighlyFragmentedOutputExactly()
        {
            const int count = 1_000_000;
            var template = new CallbackTemplate(output =>
            {
                for (var i = 0; i < count; i++)
                {
                    var span = output.GetSpan(1);
                    span[0] = (char)('a' + i % 26);
                    output.Advance(1);
                }
            });

            var result = await template.RenderAsync();

            Assert.Equal(count, result.Length);
            Assert.Equal(CreatePatternString(count), result);
        }

        [Fact]
        public async Task EnforcesMaximumOutputSizeForMixedWrites()
        {
            var template = new FluidTemplate(new CallbackStatement(output =>
            {
                output.Write("1234");
                var span = output.GetSpan(4);
                "5678".AsSpan().CopyTo(span);
                output.Advance(4);
                output.Write("9");
            }));
            var context = new TemplateContext { MaxOutputSize = 8 };

            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await template.RenderAsync(context));
        }

        [Fact]
        public async Task ExceptionAndCancellationDoNotAffectSubsequentRenders()
        {
            var throwing = new CallbackTemplate(output =>
            {
                output.Write(new string('x', 100_000));
                throw new InvalidOperationException("Expected failure.");
            });
            var cts = new CancellationTokenSource();
            var cancelling = new CallbackTemplate(output =>
            {
                output.Write(new string('y', 100_000));
                cts.Cancel();
            });

            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await throwing.RenderAsync());
            await Assert.ThrowsAsync<OperationCanceledException>(
                async () => await cancelling.RenderAsync(
                    new TemplateContext { CancellationToken = cts.Token }));

            Assert.Equal("ok", await new FluidParser().Parse("ok").RenderAsync());
        }

        [Fact]
        public async Task ConcurrentRendersOfOneTemplateWithDifferentSizesAreIsolated()
        {
            var template = new FluidParser().Parse("prefix:{{ value }}:suffix");
            var sizes = new[] { 0, 1, 99, 1_024, 16_384, 100_000, 1_000_000 };

            var tasks = Enumerable.Range(0, 32).Select(async index =>
            {
                var size = sizes[index % sizes.Length];
                var value = CreatePatternString(size);
                var context = new TemplateContext().SetValue("value", value);
                var result = await template.RenderAsync(context);
                Assert.Equal("prefix:" + value + ":suffix", result);
            });

            await Task.WhenAll(tasks);
        }

        [Fact]
        public async Task CustomTemplatesPreserveConfiguredInitialCapacity()
        {
            var observedCapacity = 0;
            var template = new CallbackTemplate(output =>
            {
                var field = output.GetType().GetField(
                    "_buffer",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                observedCapacity = ((char[])field.GetValue(output)).Length;
                output.Write("ok");
            });
            var context = new TemplateContext(
                new TemplateOptions { OutputBufferSize = 40 * 1024 });

            Assert.Equal("ok", await template.RenderAsync(context));
            Assert.True(observedCapacity >= 40 * 1024);
        }

        [Fact]
        public async Task FluidTemplatesPreserveExplicitDefaultSizedCapacity()
        {
            var observedCapacity = 0;
            var template = new FluidTemplate(new CallbackStatement(output =>
            {
                var field = output.GetType().GetField(
                    "_buffer",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                observedCapacity = ((char[])field.GetValue(output)).Length;
                output.Write("ok");
            }));
            var context = new TemplateContext(
                new TemplateOptions { OutputBufferSize = 16 * 1024 });

            Assert.Equal("ok", await template.RenderAsync(context));
            Assert.True(observedCapacity >= 16 * 1024);
        }

        private static IFluidOutputHandle CreateOutput(int initialCapacity)
        {
            var type = typeof(FluidParser).Assembly.GetType(
                "Fluid.Utils.BufferFluidOutput",
                throwOnError: true);
            var output = Activator.CreateInstance(
                type,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                args: [initialCapacity],
                culture: null);

            return new FluidOutputHandle((IFluidOutput)output, (IDisposable)output);
        }

        private static char[] CreatePattern(int length)
        {
            var result = new char[length];
            for (var i = 0; i < result.Length; i++)
            {
                result[i] = (char)('a' + i % 26);
            }

            return result;
        }

        private static string CreatePatternString(int length) => new(CreatePattern(length));

        private interface IFluidOutputHandle : IFluidOutput, IDisposable
        {
        }

        private sealed class FluidOutputHandle : IFluidOutputHandle
        {
            private readonly IFluidOutput _output;
            private readonly IDisposable _disposable;

            public FluidOutputHandle(IFluidOutput output, IDisposable disposable)
            {
                _output = output;
                _disposable = disposable;
            }

            public void Advance(int count) => _output.Advance(count);

            public Memory<char> GetMemory(int sizeHint = 0) => _output.GetMemory(sizeHint);

            public Span<char> GetSpan(int sizeHint = 0) => _output.GetSpan(sizeHint);

            public void Write(string value) => _output.Write(value);

            public void Write(char[] buffer, int index, int count) => _output.Write(buffer, index, count);

            public ValueTask FlushAsync() => _output.FlushAsync();

            public void Dispose() => _disposable.Dispose();

            public override string ToString() => _output.ToString();
        }

        private sealed class CallbackTemplate : IFluidTemplate
        {
            private readonly Action<IFluidOutput> _render;

            public CallbackTemplate(Action<IFluidOutput> render)
            {
                _render = render;
            }

            public ValueTask RenderAsync(
                IFluidOutput output,
                TextEncoder encoder,
                TemplateContext context)
            {
                _render(output);
                return default;
            }
        }

        private sealed class CallbackStatement : Statement
        {
            private readonly Action<IFluidOutput> _render;

            public CallbackStatement(Action<IFluidOutput> render)
            {
                _render = render;
            }

            public override ValueTask<Completion> WriteToAsync(
                IFluidOutput output,
                TextEncoder encoder,
                TemplateContext context)
            {
                _render(output);
                return NormalCompletion;
            }
        }

    }
}
