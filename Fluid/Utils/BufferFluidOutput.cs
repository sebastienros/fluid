using System.Buffers;
using System.Runtime.CompilerServices;

namespace Fluid.Utils
{
    /// <summary>
    /// A pooled, growable character buffer that implements <see cref="IFluidOutput"/>.
    /// Accumulates output and produces a final string via <see cref="ToString"/>.
    /// </summary>
    internal sealed class BufferFluidOutput : IFluidOutput, IDisposable
    {
        private char[] _buffer;
        private int _index;

        public BufferFluidOutput(int initialCapacity = 256)
        {
            if (initialCapacity < 0)
            {
                ExceptionHelper.ThrowArgumentOutOfRangeException(nameof(initialCapacity), "Value must be non-negative.");
            }

            _buffer = ArrayPool<char>.Shared.Rent(Math.Max(initialCapacity, 1));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Advance(int count)
        {
            if ((uint)count > (uint)(_buffer.Length - _index))
            {
                ExceptionHelper.ThrowArgumentOutOfRangeException(nameof(count), "Cannot advance beyond the buffer.");
            }

            _index += count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Memory<char> GetMemory(int sizeHint = 0)
        {
            EnsureCapacity(sizeHint);
            return _buffer.AsMemory(_index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<char> GetSpan(int sizeHint = 0)
        {
            EnsureCapacity(sizeHint);
            return _buffer.AsSpan(_index);
        }

        public void Write(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            EnsureCapacity(value.Length);
            value.CopyTo(0, _buffer, _index, value.Length);
            _index += value.Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(char value)
        {
            EnsureCapacity(1);
            _buffer[_index++] = value;
        }

        public void Write(char[] buffer, int index, int count)
        {
            ArgumentNullException.ThrowIfNull(buffer);

            if (count == 0)
            {
                return;
            }

            EnsureCapacity(count);
            buffer.AsSpan(index, count).CopyTo(_buffer.AsSpan(_index));
            _index += count;
        }

        public ValueTask FlushAsync() => default;

        public void Dispose()
        {
            var buffer = _buffer;
            if (buffer != null)
            {
                _buffer = null;
                ArrayPool<char>.Shared.Return(buffer);
            }
        }

        public override string ToString()
        {
            return _index == 0 ? string.Empty : new string(_buffer, 0, _index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureCapacity(int additionalCapacity)
        {
            if (additionalCapacity < 0)
            {
                ExceptionHelper.ThrowArgumentOutOfRangeException(nameof(additionalCapacity), "Value must be non-negative.");
            }

            if (additionalCapacity == 0)
            {
                additionalCapacity = 1;
            }

            if (_buffer.Length - _index < additionalCapacity)
            {
                Grow(additionalCapacity);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void Grow(int additionalCapacity)
        {
            // At least double, or enough for the requested capacity
            var newCapacity = Math.Max(_buffer.Length * 2, _index + additionalCapacity);

            var newBuffer = ArrayPool<char>.Shared.Rent(newCapacity);
            _buffer.AsSpan(0, _index).CopyTo(newBuffer);

            ArrayPool<char>.Shared.Return(_buffer);
            _buffer = newBuffer;
        }
    }
}
