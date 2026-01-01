using System.Buffers;

namespace Fluid.Utils
{
    internal sealed class BufferFluidOutput : IFluidOutput, IDisposable
    {
        private readonly ArrayPool<char> _pool;
        private char[] _buffer;
        private int _written;

        public BufferFluidOutput(int initialCapacity = 256)
        {
            #if NET8_0_OR_GREATER
            ArgumentOutOfRangeException.ThrowIfNegative(initialCapacity);
            #else
            if (initialCapacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(initialCapacity));
            }
            #endif

            _pool = ArrayPool<char>.Shared;
            _buffer = _pool.Rent(Math.Max(1, initialCapacity));
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

            _written += count;
            if (_written > _buffer.Length)
            {
                throw new InvalidOperationException("Advanced beyond the end of the buffer.");
            }
        }

        public Memory<char> GetMemory(int sizeHint = 0)
        {
            EnsureCapacity(sizeHint);
            return _buffer.AsMemory(_written);
        }

        public Span<char> GetSpan(int sizeHint = 0)
        {
            EnsureCapacity(sizeHint);
            return _buffer.AsSpan(_written);
        }

        public void Write(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            EnsureCapacity(value.Length);

            value.CopyTo(0, _buffer, _written, value.Length);
            _written += value.Length;
        }

        public void Write(char value)
        {
            EnsureCapacity(1);
            _buffer[_written++] = value;
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

            EnsureCapacity(count);
            Array.Copy(buffer, index, _buffer, _written, count);
            _written += count;
        }

        public ValueTask FlushAsync() => default;

        public void Dispose()
        {
            if (_buffer != null)
            {
                var toReturn = _buffer;
                _buffer = null;
                _pool.Return(toReturn);
            }
        }

        public override string ToString() => _written == 0 ? string.Empty : new string(_buffer, 0, _written);

        private void EnsureCapacity(int sizeHint)
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
                sizeHint = 1;
            }

            var available = _buffer.Length - _written;
            if (available >= sizeHint)
            {
                return;
            }

            var newSize = Math.Max(_buffer.Length * 2, _written + sizeHint);
            var newBuffer = _pool.Rent(newSize);
            Array.Copy(_buffer, 0, newBuffer, 0, _written);
            _pool.Return(_buffer);
            _buffer = newBuffer;
        }
    }
}
