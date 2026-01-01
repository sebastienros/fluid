using System.Buffers;

namespace Fluid.Utils
{
    public sealed class TextWriterFluidOutput : IFluidOutput, IDisposable
    {
        private readonly TextWriter _writer;
        private readonly bool _leaveOpen;
        private readonly ArrayPool<char> _pool;
        private char[] _buffer;
        private int _index;

        public TextWriterFluidOutput(TextWriter writer, int bufferSize, bool leaveOpen = false, ArrayPool<char> pool = null)
        {
            if (writer == null)
            {
                ExceptionHelper.ThrowArgumentNullException(nameof(writer));
            }

            #if NET8_0_OR_GREATER
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bufferSize);
            #else
            if (bufferSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bufferSize));
            }
            #endif

            _writer = writer;
            _leaveOpen = leaveOpen;
            _pool = pool ?? ArrayPool<char>.Shared;
            _buffer = _pool.Rent(bufferSize);
        }

        public void Advance(int count)
        {
            #if NET8_0_OR_GREATER
            ArgumentOutOfRangeException.ThrowIfNegative(count);
            #else
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }
            #endif

            _index += count;

            if (_index >= _buffer.Length)
            {
                FlushCore();
            }
        }

        public Memory<char> GetMemory(int sizeHint = 0)
        {
            Ensure(sizeHint);
            return _buffer.AsMemory(_index);
        }

        public Span<char> GetSpan(int sizeHint = 0)
        {
            Ensure(sizeHint);
            return _buffer.AsSpan(_index);
        }

        public void Write(char value)
        {
            if (_index == _buffer.Length)
            {
                FlushCore();
            }

            _buffer[_index++] = value;

            if (_index == _buffer.Length)
            {
                FlushCore();
            }
        }

        public void Write(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            // If the payload is larger than the buffer, flush current and write directly.
            if (value.Length >= _buffer.Length)
            {
                FlushCore();
                _writer.Write(value);
                return;
            }

            var remaining = value.Length;
            var sourceIndex = 0;

            while (remaining > 0)
            {
                var writable = _buffer.Length - _index;
                if (writable == 0)
                {
                    FlushCore();
                    writable = _buffer.Length;
                }

                var toCopy = remaining < writable ? remaining : writable;
                value.CopyTo(sourceIndex, _buffer, _index, toCopy);

                _index += toCopy;
                sourceIndex += toCopy;
                remaining -= toCopy;

                if (_index == _buffer.Length)
                {
                    FlushCore();
                }
            }
        }

        public void Write(char[] buffer, int index, int count)
        {
            if (buffer == null)
            {
                ExceptionHelper.ThrowArgumentNullException(nameof(buffer));
            }

            if (count == 0)
            {
                return;
            }

            // If the payload is larger than the buffer, flush current and write directly.
            if (count >= _buffer.Length)
            {
                FlushCore();
                _writer.Write(buffer, index, count);
                return;
            }

            var remaining = count;
            var sourceIndex = index;

            while (remaining > 0)
            {
                var writable = _buffer.Length - _index;
                if (writable == 0)
                {
                    FlushCore();
                    writable = _buffer.Length;
                }

                var toCopy = remaining < writable ? remaining : writable;
                Array.Copy(buffer, sourceIndex, _buffer, _index, toCopy);

                _index += toCopy;
                sourceIndex += toCopy;
                remaining -= toCopy;

                if (_index == _buffer.Length)
                {
                    FlushCore();
                }
            }
        }

        public ValueTask FlushAsync()
        {
            if (_index == 0)
            {
                return default;
            }

            var task = _writer.WriteAsync(_buffer, 0, _index);
            if (task.IsCompletedSuccessfully())
            {
                _index = 0;
                return default;
            }

            return Awaited(task, this);

            static async ValueTask Awaited(Task t, TextWriterFluidOutput output)
            {
                await t.ConfigureAwait(false);
                output._index = 0;
            }
        }

        public void Dispose()
        {
            if (_buffer != null)
            {
                FlushCore();

                var toReturn = _buffer;
                _buffer = null;
                _pool.Return(toReturn);
            }

            if (!_leaveOpen)
            {
                _writer.Dispose();
            }
        }

        private void Ensure(int sizeHint)
        {
            #if NET8_0_OR_GREATER
            ArgumentOutOfRangeException.ThrowIfNegative(sizeHint);
            #else
            if (sizeHint < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sizeHint));
            }
            #endif

            if (sizeHint == 0)
            {
                return;
            }

            #if NET8_0_OR_GREATER
            ArgumentOutOfRangeException.ThrowIfGreaterThan(sizeHint, _buffer.Length);
            #else
            if (sizeHint > _buffer.Length)
            {
                // Oversize requests aren't supported by this bounded-buffer implementation.
                // Producers should fall back to Write(string) for large payloads.
                throw new ArgumentOutOfRangeException(nameof(sizeHint));
            }
            #endif

            if (_buffer.Length - _index < sizeHint)
            {
                FlushCore();
            }
        }

        private void FlushCore()
        {
            if (_index == 0)
            {
                return;
            }

            _writer.Write(_buffer, 0, _index);
            _index = 0;
        }
    }
}
