using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MinimalApis.LiquidViews;
using Xunit;

namespace Fluid.Tests
{
    public class PipeWriterFluidOutputTests
    {
        [Fact]
        public async Task WritesUtf8DirectlyToPipe()
        {
            var pipe = new Pipe();
            await using var output = new PipeWriterFluidOutput(pipe.Writer, bufferSize: 16);

            output.Write("Hello, ");
            output.Write("\u4e16\u754c");
            await output.FlushAsync();

            Assert.Equal("Hello, \u4e16\u754c", await ReadAsync(pipe.Reader));
        }

        [Fact]
        public async Task PreservesSurrogatePairsAcrossWrites()
        {
            var pipe = new Pipe();
            await using var output = new PipeWriterFluidOutput(pipe.Writer, bufferSize: 16);

            output.Write("\ud83d");
            output.Write("\ude80");
            await output.FlushAsync();

            Assert.Equal("\ud83d\ude80", await ReadAsync(pipe.Reader));
        }

        [Fact]
        public async Task EncodesCharactersWrittenThroughBufferWriter()
        {
            var pipe = new Pipe();
            await using var output = new PipeWriterFluidOutput(pipe.Writer, bufferSize: 2);

            "Fluid".AsSpan().CopyTo(output.GetSpan(5));
            output.Advance(5);
            await output.FlushAsync();

            Assert.Equal("Fluid", await ReadAsync(pipe.Reader));
        }

        [Fact]
        public async Task FlushesWhenOutputBufferSizeIsReached()
        {
            var pipe = new Pipe();
            await using var output = new PipeWriterFluidOutput(pipe.Writer, bufferSize: 4);

            output.Write("content");

            Assert.Equal("content", await ReadAsync(pipe.Reader));
        }

        [Fact]
        public async Task BufferGrowthDoesNotIncreaseFlushThreshold()
        {
            var pipe = new Pipe();
            await using var output = new PipeWriterFluidOutput(pipe.Writer, bufferSize: 4);

            "data".AsSpan().CopyTo(output.GetSpan(8));
            output.Advance(4);

            Assert.Equal("data", await ReadAsync(pipe.Reader));
        }

        [Fact]
        public async Task FlushHonorsCancellation()
        {
            var pipe = new Pipe();
            using var cancellation = new CancellationTokenSource();
            var output = new PipeWriterFluidOutput(pipe.Writer, bufferSize: 16, cancellation.Token);
            output.Write("value");
            cancellation.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(() => output.FlushAsync().AsTask());
            await output.DisposeAsync();
        }

        [Fact]
        public async Task DisposeSkipsFlushAfterCancellation()
        {
            var pipe = new Pipe();
            using var cancellation = new CancellationTokenSource();
            var output = new PipeWriterFluidOutput(pipe.Writer, bufferSize: 16, cancellation.Token);
            output.Write("value");

            cancellation.Cancel();

            await output.DisposeAsync();
        }

        private static async Task<string> ReadAsync(PipeReader reader)
        {
            var result = await reader.ReadAsync();
            var value = Encoding.UTF8.GetString(result.Buffer.ToArray());
            reader.AdvanceTo(result.Buffer.End);
            return value;
        }
    }
}
