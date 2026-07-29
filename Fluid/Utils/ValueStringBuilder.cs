// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

#nullable enable

namespace System.Text
{
    internal ref partial struct ValueStringBuilder
    {
        private char[]? _arrayToReturnToPool;
        private Span<char> _chars;
        private int _pos;

        public ValueStringBuilder(Span<char> initialBuffer)
        {
            _arrayToReturnToPool = null;
            _chars = initialBuffer;
            _pos = 0;
        }

        public ValueStringBuilder(int initialCapacity)
        {
            _arrayToReturnToPool = ArrayPool<char>.Shared.Rent(initialCapacity);
            _chars = _arrayToReturnToPool;
            _pos = 0;
        }

        public int Length
        {
            get => _pos;
            set
            {
                Debug.Assert(value >= 0);
                Debug.Assert(value <= _chars.Length);
                _pos = value;
            }
        }

        public int Capacity => _chars.Length;

        public void EnsureCapacity(int capacity)
        {
            Debug.Assert(capacity >= 0);

            if ((uint)capacity > (uint)_chars.Length)
            {
                Grow(capacity - _pos);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void NullTerminate()
        {
            EnsureCapacity(_pos + 1);
            _chars[_pos] = '\0';
        }

        public ref char GetPinnableReference()
        {
            return ref MemoryMarshal.GetReference(_chars);
        }

        public ref char this[int index]
        {
            get
            {
                Debug.Assert(index < _pos);
                return ref _chars[index];
            }
        }

        public override string ToString()
        {
            var value = _chars.Slice(0, _pos).ToString();
            Dispose();
            return value;
        }

        public Span<char> RawChars => _chars;

        public ReadOnlySpan<char> AsSpan() => _chars.Slice(0, _pos);

        public ReadOnlySpan<char> AsSpan(int start) => _chars.Slice(start, _pos - start);

        public ReadOnlySpan<char> AsSpan(int start, int length) => _chars.Slice(start, length);

        public void Insert(int index, char value, int count)
        {
            if (_pos > _chars.Length - count)
            {
                Grow(count);
            }

            var remaining = _pos - index;
            _chars.Slice(index, remaining).CopyTo(_chars.Slice(index + count));
            _chars.Slice(index, count).Fill(value);
            _pos += count;
        }

        public void Insert(int index, string? value)
        {
            if (value == null)
            {
                return;
            }

            var count = value.Length;

            if (_pos > _chars.Length - count)
            {
                Grow(count);
            }

            var remaining = _pos - index;
            _chars.Slice(index, remaining).CopyTo(_chars.Slice(index + count));
            value
#if !NET
                .AsSpan()
#endif
                .CopyTo(_chars.Slice(index));
            _pos += count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(char value)
        {
            var pos = _pos;
            var chars = _chars;
            if ((uint)pos < (uint)chars.Length)
            {
                chars[pos] = value;
                _pos = pos + 1;
            }
            else
            {
                GrowAndAppend(value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(string? value)
        {
            if (value == null)
            {
                return;
            }

            var pos = _pos;
            if (value.Length == 1 && (uint)pos < (uint)_chars.Length)
            {
                _chars[pos] = value[0];
                _pos = pos + 1;
            }
            else
            {
                AppendSlow(value);
            }
        }

        private void AppendSlow(string value)
        {
            var pos = _pos;
            if (pos > _chars.Length - value.Length)
            {
                Grow(value.Length);
            }

            value
#if !NET
                .AsSpan()
#endif
                .CopyTo(_chars.Slice(pos));
            _pos += value.Length;
        }

        public void Append(char value, int count)
        {
            if (_pos > _chars.Length - count)
            {
                Grow(count);
            }

            var destination = _chars.Slice(_pos, count);
            for (var i = 0; i < destination.Length; i++)
            {
                destination[i] = value;
            }

            _pos += count;
        }

        public void Append(scoped ReadOnlySpan<char> value)
        {
            if (_pos > _chars.Length - value.Length)
            {
                Grow(value.Length);
            }

            value.CopyTo(_chars.Slice(_pos));
            _pos += value.Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<char> AppendSpan(int length)
        {
            var originalPosition = _pos;
            if (originalPosition > _chars.Length - length)
            {
                Grow(length);
            }

            _pos = originalPosition + length;
            return _chars.Slice(originalPosition, length);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void GrowAndAppend(char value)
        {
            Grow(1);
            Append(value);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void Grow(int additionalCapacityBeyondPosition)
        {
            Debug.Assert(additionalCapacityBeyondPosition > 0);
            Debug.Assert(_pos > _chars.Length - additionalCapacityBeyondPosition);

            const uint ArrayMaxLength = 0x7FFFFFC7;
            var newCapacity = (int)Math.Max(
                (uint)(_pos + additionalCapacityBeyondPosition),
                Math.Min((uint)_chars.Length * 2, ArrayMaxLength));

            var poolArray = ArrayPool<char>.Shared.Rent(newCapacity);
            _chars.Slice(0, _pos).CopyTo(poolArray);

            var toReturn = _arrayToReturnToPool;
            _chars = _arrayToReturnToPool = poolArray;
            if (toReturn != null)
            {
                ArrayPool<char>.Shared.Return(toReturn);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            var toReturn = _arrayToReturnToPool;
            this = default;
            if (toReturn != null)
            {
                ArrayPool<char>.Shared.Return(toReturn);
            }
        }
    }
}
