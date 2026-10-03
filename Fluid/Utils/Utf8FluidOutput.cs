#if NET8_0_OR_GREATER
using System.Buffers;
using System.Text;

namespace Fluid.Utils
{
    /// <summary>
    /// Transcodes Fluid's character output directly to UTF-8 in an <see cref="IBufferWriter{Byte}"/>.
    /// </summary>
    /// <remarks>
    /// The destination remains owned by the caller. In particular, this type does not flush or
    /// complete a pipe; callers should flush the destination after the outer render completes.
    /// </remarks>
    public sealed class Utf8FluidOutput : IFluidOutput, IDisposable, IAsyncDisposable
    {
        private const int MinimumUtf8BufferSize = 4;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: false);

        private readonly IBufferWriter<byte> _writer;
        private readonly Encoder _encoder;
        private readonly ArrayPool<char> _pool;
        private readonly int _minimumCharBufferSize;
        private readonly CancellationToken _cancellationToken;
        private char[] _charBuffer;
        private int _charIndex;
        private int _availableChars;
        private bool _hasWrittenChars;
        private bool _disposed;

        public Utf8FluidOutput(
            IBufferWriter<byte> writer,
            int minimumCharBufferSize = 1024,
            ArrayPool<char> pool = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(writer);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minimumCharBufferSize);

            _writer = writer;
            _encoder = Utf8.GetEncoder();
            _pool = pool ?? ArrayPool<char>.Shared;
            _minimumCharBufferSize = minimumCharBufferSize;
            _cancellationToken = cancellationToken;
        }

        public void Advance(int count)
        {
            ThrowIfDisposed();

            if ((uint)count > (uint)_availableChars)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            _availableChars = 0;
            if (count != 0)
            {
                _charIndex += count;
            }
        }

        public Memory<char> GetMemory(int sizeHint = 0)
        {
            EnsureCharBuffer(sizeHint);
            return _charBuffer.AsMemory(_charIndex);
        }

        public Span<char> GetSpan(int sizeHint = 0)
        {
            EnsureCharBuffer(sizeHint);
            return _charBuffer.AsSpan(_charIndex);
        }

        public void Write(string value)
        {
            ThrowIfDisposed();

            if (!string.IsNullOrEmpty(value))
            {
                Write(value.AsSpan());
            }
        }

        public void Write(char[] buffer, int index, int count)
        {
            ArgumentNullException.ThrowIfNull(buffer);
            ThrowIfDisposed();

            if (count != 0)
            {
                Write(buffer.AsSpan(index, count));
            }
        }

        public ValueTask FlushAsync()
        {
            ThrowIfDisposed();
            _cancellationToken.ThrowIfCancellationRequested();
            FlushCharBuffer();
            return default;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                if (!_cancellationToken.IsCancellationRequested)
                {
                    FlushCharBuffer();
                    FinishEncoding();
                }
            }
            finally
            {
                DisposeCore();
            }
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return default;
        }

        private void Encode(ReadOnlySpan<char> source, bool flush)
        {
            _cancellationToken.ThrowIfCancellationRequested();

            if (!flush)
            {
                _hasWrittenChars = true;
            }

            bool completed;
            do
            {
                var destination = _writer.GetSpan(MinimumUtf8BufferSize);
                _encoder.Convert(
                    source,
                    destination,
                    flush,
                    out var charsUsed,
                    out var bytesUsed,
                    out completed);

                if (bytesUsed != 0)
                {
                    _writer.Advance(bytesUsed);
                }

                source = source.Slice(charsUsed);

                if (!completed && charsUsed == 0 && bytesUsed == 0)
                {
                    throw new InvalidOperationException("The UTF-8 destination did not provide enough writable memory.");
                }
            }
            while (!completed);
        }

        private void FinishEncoding()
        {
            if (_hasWrittenChars)
            {
                Encode(ReadOnlySpan<char>.Empty, flush: true);
                _encoder.Reset();
                _hasWrittenChars = false;
            }
        }

        private void EnsureCharBuffer(int sizeHint)
        {
            ThrowIfDisposed();
            ArgumentOutOfRangeException.ThrowIfNegative(sizeHint);

            if (sizeHint == 0)
            {
                sizeHint = 1;
            }

            if (_charBuffer != null && _charBuffer.Length - _charIndex >= sizeHint)
            {
                _availableChars = _charBuffer.Length - _charIndex;
                return;
            }

            FlushCharBuffer();

            var required = Math.Max(sizeHint, _minimumCharBufferSize);
            if (_charBuffer == null || _charBuffer.Length < required)
            {
                var replacement = _pool.Rent(required);
                if (_charBuffer != null)
                {
                    _pool.Return(_charBuffer);
                }

                _charBuffer = replacement;
            }

            _availableChars = _charBuffer.Length - _charIndex;
        }

        private void Write(ReadOnlySpan<char> value)
        {
            if (_charBuffer == null)
            {
                Encode(value, flush: false);
                return;
            }

            if (value.Length >= _charBuffer.Length)
            {
                FlushCharBuffer();
                Encode(value, flush: false);
                return;
            }

            if (_charBuffer.Length - _charIndex < value.Length)
            {
                FlushCharBuffer();
            }

            value.CopyTo(_charBuffer.AsSpan(_charIndex));
            _charIndex += value.Length;
        }

        private void FlushCharBuffer()
        {
            if (_charIndex != 0)
            {
                Encode(_charBuffer.AsSpan(0, _charIndex), flush: false);
                _charIndex = 0;
            }
        }

        private void DisposeCore()
        {
            _disposed = true;
            _charIndex = 0;
            _availableChars = 0;

            if (_charBuffer != null)
            {
                _pool.Return(_charBuffer);
                _charBuffer = null;
            }
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
        }
    }
}
#endif
