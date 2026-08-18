using Fluid.Tests.Mocks;
using Fluid.Utils;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Fluid.Tests
{
    public class Utf8FluidOutputTests
    {
#if COMPILED
        private static readonly FluidParser _parser = new FluidParser().Compile();
#else
        private static readonly FluidParser _parser = new FluidParser();
#endif

        [Fact]
        public async Task WritesAsciiBmpAndNonBmpUnicode()
        {
            var writer = new ArrayBufferWriter<byte>();
            await using var output = new Utf8FluidOutput(writer);

            output.Write("ASCII | \u4e16\u754c | \ud83d\ude80");
            await output.FlushAsync();

            Assert.Equal("ASCII | \u4e16\u754c | \ud83d\ude80", Decode(writer));
        }

        [Fact]
        public async Task PreservesSurrogatePairsAcrossWrites()
        {
            var writer = new ArrayBufferWriter<byte>();
            await using var output = new Utf8FluidOutput(writer);

            output.Write("\ud83d");
            await output.FlushAsync();
            output.Write("\ude80");
            await output.FlushAsync();

            Assert.Equal("\ud83d\ude80", Decode(writer));
        }

        [Fact]
        public async Task ContinuesWritingAfterIntermediateFlush()
        {
            var writer = new ArrayBufferWriter<byte>();
            await using var output = new Utf8FluidOutput(writer);

            output.Write("\u4e16\u754c");
            await output.FlushAsync();
            output.Write("\ud83d\ude80");
            await output.FlushAsync();

            Assert.Equal("\u4e16\u754c\ud83d\ude80", Decode(writer));
        }

        [Fact]
        public async Task ReplacesMalformedSurrogatesLikeStandardUtf8()
        {
            var writer = new ArrayBufferWriter<byte>();
            var output = new Utf8FluidOutput(writer);

            output.Write("\ud83dX\ude80");
            await output.DisposeAsync();

            Assert.Equal("\ufffdX\ufffd", Decode(writer));
        }

        [Fact]
        public async Task EncodesCharactersWrittenThroughBufferWriter()
        {
            var writer = new ArrayBufferWriter<byte>();
            await using var output = new Utf8FluidOutput(writer, minimumCharBufferSize: 2);

            "Fluid \ud83d\ude80".AsSpan().CopyTo(output.GetSpan(8));
            output.Advance(8);
            await output.FlushAsync();

            Assert.Equal("Fluid \ud83d\ude80", Decode(writer));
        }

        [Fact]
        public async Task RendersLiteralsEncodedValuesAndRawValuesWithCorrectSemantics()
        {
            var template = _parser.Parse("<p>{{ value }}|{{ value | raw }}</p>");
            var context = new TemplateContext().SetValue("value", "<\ud83d\ude80>");
            var writer = new ArrayBufferWriter<byte>();

            await template.RenderAsync(writer, HtmlEncoder.Default, context);

            Assert.Equal("<p>&lt;&#x1F680;&gt;|<\ud83d\ude80></p>", Decode(writer));
        }

        [Fact]
        public async Task RenderAsyncFinalizesEncodingWithoutOwningDestination()
        {
            var template = _parser.Parse("{{ value | raw }}");
            var writer = new DisposableByteWriter();

            await template.RenderAsync(
                writer,
                NullEncoder.Default,
                new TemplateContext().SetValue("value", "\ud83d"));

            Assert.Equal("\ufffd", writer.ToString());
            Assert.False(writer.IsDisposed);
        }

        [Fact]
        public async Task RenderAsyncReportsCancellationRaisedDuringFinalization()
        {
            using var cancellation = new CancellationTokenSource();
            var writer = new CancellationOnSecondGetSpanWriter(cancellation);

            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                _parser.Parse("value").RenderAsync(
                    writer,
                    NullEncoder.Default,
                    new TemplateContext { CancellationToken = cancellation.Token }).AsTask());
        }

        [Fact]
        public async Task TranscodesAcrossTinyDestinationSegments()
        {
            var writer = new SegmentedByteWriter(segmentSize: 4);
            await using var output = new Utf8FluidOutput(writer);

            output.Write("a\u00e9\u4e16\ud83d\ude80z");
            await output.FlushAsync();

            Assert.Equal("a\u00e9\u4e16\ud83d\ude80z", writer.ToString());
            Assert.True(writer.AdvanceCount > 1);
        }

        [Fact]
        public async Task FlushHonorsCancellation()
        {
            var writer = new ArrayBufferWriter<byte>();
            using var cancellation = new CancellationTokenSource();
            var output = new Utf8FluidOutput(writer, cancellationToken: cancellation.Token);
            output.Write("value");
            cancellation.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(() => output.FlushAsync().AsTask());
            await output.DisposeAsync();
        }

        [Fact]
        public async Task DisposalSkipsBufferedEncodingAfterCancellation()
        {
            var writer = new ArrayBufferWriter<byte>();
            using var cancellation = new CancellationTokenSource();
            var output = new Utf8FluidOutput(
                writer,
                minimumCharBufferSize: 4,
                cancellationToken: cancellation.Token);
            "data".AsSpan().CopyTo(output.GetSpan(4));
            output.Advance(4);

            cancellation.Cancel();
            await output.DisposeAsync();

            Assert.Equal(0, writer.WrittenCount);
        }

        [Fact]
        public async Task PipeBackpressureIsAwaitedOnlyAtTheTransportBoundary()
        {
            var pipe = new Pipe(new PipeOptions(
                pauseWriterThreshold: 1,
                resumeWriterThreshold: 1,
                useSynchronizationContext: false));
            await using var output = new Utf8FluidOutput(pipe.Writer);
            output.Write(new string('x', 32));

            await output.FlushAsync();
            var transportFlush = pipe.Writer.FlushAsync();
            Assert.False(transportFlush.IsCompletedSuccessfully);

            var read = await pipe.Reader.ReadAsync();
            Assert.Equal(32, read.Buffer.Length);
            pipe.Reader.AdvanceTo(read.Buffer.End);
            Assert.False((await transportFlush).IsCanceled);
        }

        [Fact]
        public async Task NestedRenderUsesOneContinuousUtf8Output()
        {
            var fileProvider = new MockFileProvider().Add("item.liquid", "[{{ item }}]");
            var context = new TemplateContext(new TemplateOptions { FileProvider = fileProvider });
            context.SetValue("items", new[] { "\ud83d\ude80", "\u4e16\u754c" });
            var template = _parser.Parse("{% render 'item' for items as item %}");
            var writer = new ArrayBufferWriter<byte>();

            await template.RenderAsync(writer, NullEncoder.Default, context);

            Assert.Equal("[\ud83d\ude80][\u4e16\u754c]", Decode(writer));
        }

        [Fact]
        public async Task MaxOutputSizeRemainsAUtf16CharacterLimit()
        {
            var template = _parser.Parse("{{ value }}");
            var writer = new ArrayBufferWriter<byte>();

            await template.RenderAsync(
                writer,
                NullEncoder.Default,
                new TemplateContext { MaxOutputSize = 2 }.SetValue("value", "\ud83d\ude80"));

            Assert.Equal(4, writer.WrittenCount);
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                template.RenderAsync(
                    new ArrayBufferWriter<byte>(),
                    NullEncoder.Default,
                    new TemplateContext { MaxOutputSize = 1 }.SetValue("value", "\ud83d\ude80")).AsTask());
        }

        [Fact]
        public async Task DisposalFinalizesEncodingWithoutOwningDestination()
        {
            var writer = new DisposableByteWriter();
            var output = new Utf8FluidOutput(writer);
            output.Write("\ud83d");

            await output.DisposeAsync();

            Assert.Equal("\ufffd", writer.ToString());
            Assert.False(writer.IsDisposed);
            Assert.Throws<ObjectDisposedException>(() => output.Write("x"));
        }

        private static string Decode(ArrayBufferWriter<byte> writer) =>
            Encoding.UTF8.GetString(writer.WrittenSpan);

        private class SegmentedByteWriter : IBufferWriter<byte>
        {
            private readonly int _segmentSize;
            private readonly List<byte> _bytes = new();
            private byte[] _current;

            public SegmentedByteWriter(int segmentSize)
            {
                _segmentSize = segmentSize;
            }

            public int AdvanceCount { get; private set; }

            public void Advance(int count)
            {
                AdvanceCount++;
                for (var i = 0; i < count; i++)
                {
                    _bytes.Add(_current[i]);
                }
            }

            public Memory<byte> GetMemory(int sizeHint = 0)
            {
                _current = new byte[Math.Max(_segmentSize, sizeHint)];
                return _current;
            }

            public Span<byte> GetSpan(int sizeHint = 0) => GetMemory(sizeHint).Span;

            public override string ToString() => Encoding.UTF8.GetString(_bytes.ToArray());
        }

        private sealed class DisposableByteWriter : SegmentedByteWriter, IDisposable
        {
            public DisposableByteWriter()
                : base(4)
            {
            }

            public bool IsDisposed { get; private set; }

            public void Dispose() => IsDisposed = true;
        }

        private sealed class CancellationOnSecondGetSpanWriter : IBufferWriter<byte>
        {
            private readonly ArrayBufferWriter<byte> _inner = new();
            private readonly CancellationTokenSource _cancellation;
            private int _getSpanCount;

            public CancellationOnSecondGetSpanWriter(CancellationTokenSource cancellation)
            {
                _cancellation = cancellation;
            }

            public void Advance(int count) => _inner.Advance(count);

            public Memory<byte> GetMemory(int sizeHint = 0)
            {
                BeforeGetBuffer();
                return _inner.GetMemory(sizeHint);
            }

            public Span<byte> GetSpan(int sizeHint = 0)
            {
                BeforeGetBuffer();
                return _inner.GetSpan(sizeHint);
            }

            private void BeforeGetBuffer()
            {
                if (++_getSpanCount == 2)
                {
                    _cancellation.Cancel();
                }
            }
        }
    }
}
