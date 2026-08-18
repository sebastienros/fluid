using Fluid;
using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MinimalApis.LiquidViews
{
    /// <summary>
    /// Writes Fluid output as UTF-8 directly to a <see cref="PipeWriter"/>.
    /// </summary>
    public sealed class PipeWriterFluidOutput : IFluidOutput, IAsyncDisposable
    {
        private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        private readonly PipeWriter _writer;
        private readonly Encoder _encoder;
        private readonly CancellationToken _cancellationToken;
        private readonly int _flushThreshold;
        private char[] _buffer;
        private int _bufferCapacity;
        private int _index;
        private bool _disposed;
        private bool _encoderNeedsFlush;
        private int _unflushedBytes;

        public PipeWriterFluidOutput(
            PipeWriter writer,
            int bufferSize,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(writer);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bufferSize);

            _writer = writer;
            _encoder = Utf8.GetEncoder();
            _cancellationToken = cancellationToken;
            _flushThreshold = bufferSize;
            _buffer = ArrayPool<char>.Shared.Rent(bufferSize);
            _bufferCapacity = bufferSize;
        }

        public void Advance(int count)
        {
            ThrowIfDisposed();

            if ((uint) count > (uint) (_bufferCapacity - _index))
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            _index += count;

            if (_index >= _flushThreshold)
            {
                FlushBuffer();
            }
        }

        public Memory<char> GetMemory(int sizeHint = 0)
        {
            EnsureBuffer(sizeHint);
            return _buffer.AsMemory(_index, _bufferCapacity - _index);
        }

        public Span<char> GetSpan(int sizeHint = 0)
        {
            EnsureBuffer(sizeHint);
            return _buffer.AsSpan(_index, _bufferCapacity - _index);
        }

        public void Write(string value)
        {
            ThrowIfDisposed();

            if (!String.IsNullOrEmpty(value))
            {
                if (value.Length >= _flushThreshold)
                {
                    FlushBuffer();
                    Encode(value.AsSpan(), flush: false);
                    return;
                }

                if (_bufferCapacity - _index < value.Length)
                {
                    FlushBuffer();
                }

                value.AsSpan().CopyTo(_buffer.AsSpan(_index));
                _index += value.Length;

                if (_index >= _flushThreshold)
                {
                    FlushBuffer();
                }
            }
        }

        public void Write(char[] buffer, int index, int count)
        {
            ArgumentNullException.ThrowIfNull(buffer);
            ThrowIfDisposed();

            if (count != 0)
            {
                var source = buffer.AsSpan(index, count);

                if (count >= _flushThreshold)
                {
                    FlushBuffer();
                    Encode(source, flush: false);
                    return;
                }

                if (_bufferCapacity - _index < count)
                {
                    FlushBuffer();
                }

                source.CopyTo(_buffer.AsSpan(_index));
                _index += count;

                if (_index >= _flushThreshold)
                {
                    FlushBuffer();
                }
            }
        }

        public ValueTask FlushAsync()
        {
            ThrowIfDisposed();
            _cancellationToken.ThrowIfCancellationRequested();

            if (!NeedsFlush)
            {
                return default;
            }

            FlushBuffer();
            Encode(ReadOnlySpan<char>.Empty, flush: true);
            _encoder.Reset();

            var flush = _writer.FlushAsync(_cancellationToken);
            if (flush.IsCompletedSuccessfully)
            {
                ThrowIfCanceled(flush.Result);
                _encoderNeedsFlush = false;
                _unflushedBytes = 0;
                return default;
            }

            return Awaited(flush, this);

            static async ValueTask Awaited(ValueTask<FlushResult> flush, PipeWriterFluidOutput output)
            {
                ThrowIfCanceled(await flush.ConfigureAwait(false), output._cancellationToken);
                output._encoderNeedsFlush = false;
                output._unflushedBytes = 0;
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                if (NeedsFlush && !_cancellationToken.IsCancellationRequested)
                {
                    await FlushAsync().ConfigureAwait(false);
                }
            }
            finally
            {
                _disposed = true;
                var buffer = _buffer;
                _buffer = null;
                ArrayPool<char>.Shared.Return(buffer);
            }
        }

        private static void ThrowIfCanceled(FlushResult result, CancellationToken cancellationToken = default)
        {
            if (result.IsCanceled)
            {
                throw new OperationCanceledException(cancellationToken);
            }
        }

        private void Encode(ReadOnlySpan<char> source, bool flush)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            var encoderNeedsFlush = !flush && !source.IsEmpty && char.IsHighSurrogate(source[source.Length - 1]);

            do
            {
                var charCount = Math.Min(source.Length, 1024);
                var destination = _writer.GetSpan(Utf8.GetMaxByteCount(Math.Max(charCount, 1)));

                _encoder.Convert(
                    source,
                    destination,
                    flush,
                    out var charsUsed,
                    out var bytesUsed,
                    out var completed);

                if (bytesUsed != 0)
                {
                    _writer.Advance(bytesUsed);
                    _unflushedBytes += bytesUsed;

                    if (!flush && _unflushedBytes >= _flushThreshold)
                    {
                        FlushPipeSynchronously();
                    }
                }

                source = source.Slice(charsUsed);

                if (completed)
                {
                    _encoderNeedsFlush = encoderNeedsFlush;
                    return;
                }
            }
            while (!source.IsEmpty || flush);
        }

        private void FlushPipeSynchronously()
        {
            var flush = _writer.FlushAsync(_cancellationToken);
            var result = flush.IsCompletedSuccessfully
                ? flush.Result
                : flush.AsTask().GetAwaiter().GetResult();

            ThrowIfCanceled(result, _cancellationToken);
            _unflushedBytes = 0;
        }

        private void FlushBuffer()
        {
            if (_index == 0)
            {
                return;
            }

            Encode(_buffer.AsSpan(0, _index), flush: false);
            _index = 0;
        }

        private void EnsureBuffer(int sizeHint)
        {
            ThrowIfDisposed();
            ArgumentOutOfRangeException.ThrowIfNegative(sizeHint);

            if (sizeHint == 0)
            {
                sizeHint = 1;
            }

            if (sizeHint <= _bufferCapacity - _index)
            {
                return;
            }

            FlushBuffer();

            if (sizeHint <= _bufferCapacity)
            {
                return;
            }

            var newBuffer = ArrayPool<char>.Shared.Rent(sizeHint);
            ArrayPool<char>.Shared.Return(_buffer);
            _buffer = newBuffer;
            _bufferCapacity = sizeHint;
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
        }

        private bool NeedsFlush => _index != 0 || _unflushedBytes != 0 || _encoderNeedsFlush;
    }
}
