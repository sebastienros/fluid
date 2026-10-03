using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Fluid.Tests.MvcViewEngine
{
    /// <summary>
    /// Stream implementation that prevents non-async usages.
    /// </summary>
    public sealed class NoSyncStream : Stream
    {
        public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => Task.CompletedTask;
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> source, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;


        public override void Flush() => throw new InvalidOperationException();
        public override int Read(byte[] buffer, int offset, int count) => throw new InvalidOperationException();
        public override long Seek(long offset, SeekOrigin origin) => throw new InvalidOperationException();
        public override void SetLength(long value) => throw new InvalidOperationException();
        public override void Write(byte[] buffer, int offset, int count) => throw new InvalidOperationException();

        public override bool CanRead { get; } = false;
        public override bool CanSeek { get; } = false;
        public override bool CanWrite { get; } = true;
        public override long Length { get; }
        public override long Position { get; set; }
    }
}
