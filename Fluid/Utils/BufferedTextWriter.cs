using System.Buffers;
using System.Text;
using Fluid;

namespace Fluid.Utils
{
    internal sealed class BufferedTextWriter : TextWriter
    {
        private readonly TextWriter _inner;
        private readonly bool _leaveOpen;
        private readonly ArrayPool<char> _pool;
        private char[] _buffer;
        private int _index;

        public BufferedTextWriter(TextWriter inner, int bufferSize, bool leaveOpen = false, ArrayPool<char> pool = null)
        {
            if (inner == null)
            {
                ExceptionHelper.ThrowArgumentNullException(nameof(inner));
            }

            #if NET8_0_OR_GREATER
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bufferSize);
            #else
            if (bufferSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bufferSize));
            }
            #endif

            _inner = inner;
            _leaveOpen = leaveOpen;
            _pool = pool ?? ArrayPool<char>.Shared;
            _buffer = _pool.Rent(bufferSize);
        }

        public override Encoding Encoding => _inner.Encoding;

        public int Capacity => _buffer.Length;

        public override void Write(char value)
        {
            if (_index == _buffer.Length)
            {
                FlushBuffer();
            }

            _buffer[_index++] = value;
        }

        public override void Write(char[] buffer, int index, int count)
        {
            if (buffer == null)
            {
                ExceptionHelper.ThrowArgumentNullException(nameof(buffer));
            }

            if (count == 0)
            {
                return;
            }

            WriteCore(buffer, index, count);
        }

        public override void Write(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            // Avoid span APIs for netstandard2.0.
            var remaining = value.Length;
            var sourceIndex = 0;

            // If it's larger than our entire buffer, flush current content and write directly.
            if (remaining >= _buffer.Length)
            {
                FlushBuffer();
                _inner.Write(value);
                return;
            }

            while (remaining > 0)
            {
                var writable = _buffer.Length - _index;
                if (writable == 0)
                {
                    FlushBuffer();
                    writable = _buffer.Length;
                }

                var toCopy = remaining < writable ? remaining : writable;

                #if NET8_0_OR_GREATER
                value.AsSpan(sourceIndex, toCopy).CopyTo(_buffer.AsSpan(_index));
                #else
                value.CopyTo(sourceIndex, _buffer, _index, toCopy);
                #endif

                _index += toCopy;
                sourceIndex += toCopy;
                remaining -= toCopy;
            }
        }

        public override Task WriteAsync(string value)
        {
            // Buffer synchronously; avoid Task allocations.
            Write(value);
            return Task.CompletedTask;
        }

        public override Task WriteAsync(char[] buffer, int index, int count)
        {
            Write(buffer, index, count);
            return Task.CompletedTask;
        }

        public override void Flush()
        {
            FlushBuffer();
            _inner.Flush();
        }

        public override async Task FlushAsync()
        {
            if (_index > 0)
            {
                await _inner.WriteAsync(_buffer, 0, _index).ConfigureAwait(false);
                _index = 0;
            }

            await _inner.FlushAsync().ConfigureAwait(false);
        }

        private void WriteCore(char[] buffer, int index, int count)
        {
            if (count >= _buffer.Length)
            {
                FlushBuffer();
                _inner.Write(buffer, index, count);
                return;
            }

            var remaining = count;
            var sourceIndex = index;

            while (remaining > 0)
            {
                var writable = _buffer.Length - _index;
                if (writable == 0)
                {
                    FlushBuffer();
                    writable = _buffer.Length;
                }

                var toCopy = remaining < writable ? remaining : writable;

                #if NET8_0_OR_GREATER
                buffer.AsSpan(sourceIndex, toCopy).CopyTo(_buffer.AsSpan(_index));
                #else
                Array.Copy(buffer, sourceIndex, _buffer, _index, toCopy);
                #endif

                _index += toCopy;
                sourceIndex += toCopy;
                remaining -= toCopy;
            }
        }

        private void FlushBuffer()
        {
            if (_index == 0)
            {
                return;
            }

            _inner.Write(_buffer, 0, _index);
            _index = 0;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_buffer != null)
                {
                    FlushBuffer();

                    var toReturn = _buffer;
                    _buffer = null;
                    _pool.Return(toReturn);
                }

                if (!_leaveOpen)
                {
                    _inner.Dispose();
                }
            }

            base.Dispose(disposing);
        }
    }
}
