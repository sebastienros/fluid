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
        internal const int DefaultInitialCapacity = 256;
        internal const int MaximumContiguousCapacity = 32 * 1024;
        internal const int MaximumSegmentCapacity = 32 * 1024;

        private char[] _buffer;
        private Segment[] _segments;
        private int _index;

        public BufferFluidOutput(int initialCapacity = DefaultInitialCapacity)
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

            if (value.Length <= _buffer.Length - _index)
            {
                value.CopyTo(0, _buffer, _index, value.Length);
                _index += value.Length;
                return;
            }

            WriteSlow(value.AsSpan());
        }

        public void Write(char[] buffer, int index, int count)
        {
            ArgumentNullException.ThrowIfNull(buffer);

            if (count == 0)
            {
                return;
            }

            if (count <= _buffer.Length - _index)
            {
                buffer.AsSpan(index, count).CopyTo(_buffer.AsSpan(_index));
                _index += count;
                return;
            }

            WriteSlow(buffer.AsSpan(index, count));
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

            var segments = _segments;
            if (segments != null)
            {
                var segmentCount = segments[0].Length;
                for (var i = 1; i <= segmentCount; i++)
                {
                    ArrayPool<char>.Shared.Return(segments[i].Buffer);
                }

                _segments = null;
                ArrayPool<Segment>.Shared.Return(segments, clearArray: true);
            }
        }

        public override string ToString()
        {
            var segmentCount = _segments?[0].Length ?? 0;
            var length = _index;
            for (var i = 1; i <= segmentCount; i++)
            {
                length += _segments[i].Length;
            }

            if (length == 0)
            {
                return string.Empty;
            }

            if (segmentCount == 0)
            {
                return new string(_buffer, 0, _index);
            }

#if NET6_0_OR_GREATER
            return string.Create(length, this, static (destination, output) => output.CopyTo(destination));
#else
            unsafe
            {
                var result = new string('\0', length);
                fixed (char* destination = result)
                {
                    CopyTo(new Span<char>(destination, length));
                }

                return result;
            }
#endif
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
                if (_segments == null &&
                    _index <= MaximumContiguousCapacity - additionalCapacity)
                {
                    GrowContiguous(additionalCapacity);
                }
                else
                {
                    AddSegment(additionalCapacity);
                }
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void GrowContiguous(int sizeHint)
        {
            var oldBuffer = _buffer;
            var newBuffer = ArrayPool<char>.Shared.Rent(
                GetNextCapacity(oldBuffer.Length, _index + sizeHint));
            oldBuffer.AsSpan(0, _index).CopyTo(newBuffer);
            _buffer = newBuffer;
            ArrayPool<char>.Shared.Return(oldBuffer);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void AddSegment(int sizeHint)
        {
            if (_index == 0 && _segments == null)
            {
                var emptyBuffer = _buffer;
                _buffer = ArrayPool<char>.Shared.Rent(
                    GetNextCapacity(emptyBuffer.Length, sizeHint));
                ArrayPool<char>.Shared.Return(emptyBuffer);
                return;
            }

            var committedBuffer = _buffer;
            var newBuffer = ArrayPool<char>.Shared.Rent(
                GetNextCapacity(committedBuffer.Length, sizeHint));
            try
            {
                AddCommittedSegment(committedBuffer, _index);
            }
            catch
            {
                ArrayPool<char>.Shared.Return(newBuffer);
                throw;
            }

            _buffer = newBuffer;
            _index = 0;
        }

        private void AddCommittedSegment(char[] buffer, int length)
        {
            var segments = _segments;
            if (segments == null)
            {
                segments = ArrayPool<Segment>.Shared.Rent(4);
                _segments = segments;
            }

            var segmentCount = segments[0].Length;
            if (segmentCount + 1 == segments.Length)
            {
                var newSegments = ArrayPool<Segment>.Shared.Rent(segments.Length * 2);
                segments.AsSpan().CopyTo(newSegments);
                ArrayPool<Segment>.Shared.Return(segments, clearArray: true);
                _segments = segments = newSegments;
            }

            segments[++segmentCount] = new Segment(buffer, length);
            segments[0] = new Segment(null, segmentCount);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteSlow(ReadOnlySpan<char> value)
        {
            if (_index == 0 && _segments == null && value.Length > _buffer.Length)
            {
                var emptyBuffer = _buffer;
                var capacity = Math.Min(value.Length, MaximumSegmentCapacity);
                if (capacity > emptyBuffer.Length)
                {
                    _buffer = ArrayPool<char>.Shared.Rent(capacity);
                    ArrayPool<char>.Shared.Return(emptyBuffer);
                }
            }
            else if (_segments == null &&
                value.Length > _buffer.Length - _index &&
                _index <= MaximumContiguousCapacity - value.Length)
            {
                GrowContiguous(value.Length);
            }

            while (!value.IsEmpty)
            {
                var remaining = _buffer.Length - _index;
                if (remaining == 0)
                {
                    AddSegment(Math.Min(value.Length, MaximumSegmentCapacity));
                    remaining = _buffer.Length;
                }

                var count = Math.Min(value.Length, remaining);
                value.Slice(0, count).CopyTo(_buffer.AsSpan(_index));
                _index += count;
                value = value.Slice(count);
            }
        }

        private void CopyTo(Span<char> destination)
        {
            var offset = 0;
            var segmentCount = _segments[0].Length;
            for (var i = 1; i <= segmentCount; i++)
            {
                var segment = _segments[i];
                segment.Buffer.AsSpan(0, segment.Length).CopyTo(destination.Slice(offset));
                offset += segment.Length;
            }

            _buffer.AsSpan(0, _index).CopyTo(destination.Slice(offset));
        }

        private static int GetNextCapacity(int currentCapacity, int sizeHint)
        {
            if (sizeHint > MaximumSegmentCapacity)
            {
                return sizeHint;
            }

            var growth = currentCapacity <= MaximumSegmentCapacity / 4
                ? currentCapacity * 4
                : MaximumSegmentCapacity;

            return Math.Max(sizeHint, growth);
        }

        private readonly struct Segment
        {
            public Segment(char[] buffer, int length)
            {
                Buffer = buffer;
                Length = length;
            }

            public char[] Buffer { get; }

            public int Length { get; }
        }
    }
}
