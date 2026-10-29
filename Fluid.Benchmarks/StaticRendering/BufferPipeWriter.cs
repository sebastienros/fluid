using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace Fluid.Benchmarks.StaticRendering;

/// <summary>
/// A <see cref="PipeWriter"/> over a reusable array, like a response body that doesn't cost anything to write to.
/// It is also an <see cref="System.Buffers.IBufferWriter{T}"/>, so every engine writes UTF-8 to the same kind of sink.
/// </summary>
internal sealed class BufferPipeWriter : PipeWriter
{
    private byte[] _buffer = new byte[64 * 1024];
    private int _written;

    public ReadOnlySpan<byte> WrittenSpan => _buffer.AsSpan(0, _written);

    public void Reset() => _written = 0;

    public override void Advance(int bytes) => _written += bytes;

    public override Memory<byte> GetMemory(int sizeHint = 0)
    {
        EnsureCapacity(sizeHint);
        return _buffer.AsMemory(_written);
    }

    public override Span<byte> GetSpan(int sizeHint = 0)
    {
        EnsureCapacity(sizeHint);
        return _buffer.AsSpan(_written);
    }

    private void EnsureCapacity(int sizeHint)
    {
        if (_buffer.Length - _written < Math.Max(sizeHint, 1))
        {
            Array.Resize(ref _buffer, Math.Max(_buffer.Length * 2, _written + sizeHint));
        }
    }

    public override ValueTask<FlushResult> FlushAsync(CancellationToken cancellationToken = default) => new(new FlushResult(false, false));

    public override void CancelPendingFlush()
    {
    }

    public override void Complete(Exception exception = null)
    {
    }
}
