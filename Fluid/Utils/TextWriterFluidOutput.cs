using System.Buffers;

namespace Fluid.Utils
{
    public sealed class TextWriterFluidOutput : IFluidOutput, IDisposable, IAsyncDisposable
    {
        private readonly TextWriter _writer;
        private readonly bool _leaveOpen;
        private readonly bool _allowSynchronousIO;
        private readonly ArrayPool<char> _pool;
        private char[] _buffer;
        private int _index;

        public TextWriterFluidOutput(TextWriter writer, int bufferSize, bool leaveOpen = false, ArrayPool<char> pool = null, bool allowSynchronousIO = true)
        {
            ArgumentNullException.ThrowIfNull(writer);

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
            _allowSynchronousIO = allowSynchronousIO;
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
                FlushCoreWithPolicy();
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

        public void Write(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            // If the payload is larger than the buffer, flush current and write directly.
            if (value.Length >= _buffer.Length)
            {
                FlushCoreWithPolicy();
                WriteDirect(value);
                return;
            }

            WriteBuffered(value.AsSpan());
        }

        public void Write(char[] buffer, int index, int count)
        {
            ArgumentNullException.ThrowIfNull(buffer);

            if (count == 0)
            {
                return;
            }

            // If the payload is larger than the buffer, flush current and write directly.
            if (count >= _buffer.Length)
            {
                FlushCoreWithPolicy();
                WriteDirect(buffer, index, count);
                return;
            }

            WriteBuffered(buffer.AsSpan(index, count));
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
                FlushCoreSync();

                var toReturn = _buffer;
                _buffer = null;
                _pool.Return(toReturn);
            }

            if (!_leaveOpen)
            {
                _writer.Dispose();
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_buffer != null)
            {
                await FlushAsync();

                var toReturn = _buffer;
                _buffer = null;
                _pool.Return(toReturn);
            }

            if (!_leaveOpen)
            {
#if NETSTANDARD2_0
                _writer.Dispose();
#else
                await _writer.DisposeAsync();
#endif
            }
        }

        private void Ensure(int sizeHint)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(sizeHint);

            if (sizeHint == 0)
            {
                return;
            }

            ArgumentOutOfRangeException.ThrowIfGreaterThan(sizeHint, _buffer.Length);

            if (_buffer.Length - _index < sizeHint)
            {
                FlushCoreWithPolicy();
            }
        }

        // Copies source chars into the internal buffer and flushes when full.
        // This is the common buffered write path used by string and char[] overloads.
        private void WriteBuffered(ReadOnlySpan<char> source)
        {
            while (!source.IsEmpty)
            {
                if (_index == _buffer.Length)
                {
                    FlushCoreWithPolicy();
                }

                var writable = Math.Min(_buffer.Length - _index, source.Length);
                source.Slice(0, writable).CopyTo(_buffer.AsSpan(_index, writable));

                _index += writable;
                source = source.Slice(writable);
            }

            if (_index == _buffer.Length)
            {
                FlushCoreWithPolicy();
            }
        }

        // Completes an async write from synchronous code paths and preserves original exceptions.
        private static void CompleteSynchronously(Task task)
        {
            if (!task.IsCompletedSuccessfully())
            {
                task.GetAwaiter().GetResult();
            }
        }

        // Flushes buffered data using the configured IO policy:
        // sync writes when allowed, async API when synchronous IO is disallowed.
        private void FlushCoreWithPolicy()
        {
            if (_index == 0)
            {
                return;
            }

            if (_allowSynchronousIO)
            {
                _writer.Write(_buffer, 0, _index);
            }
            else
            {
                CompleteSynchronously(_writer.WriteAsync(_buffer, 0, _index));
            }

            _index = 0;
        }

        // Always performs a synchronous flush. Used by synchronous Dispose() semantics.
        private void FlushCoreSync()
        {
            if (_index == 0)
            {
                return;
            }

            _writer.Write(_buffer, 0, _index);
            _index = 0;
        }

        // Writes large string payloads directly to the underlying writer, bypassing the buffer.
        private void WriteDirect(string value)
        {
            if (_allowSynchronousIO)
            {
                _writer.Write(value);
            }
            else
            {
                CompleteSynchronously(_writer.WriteAsync(value));
            }
        }

        // Writes large char[] payloads directly to the underlying writer, bypassing the buffer.
        private void WriteDirect(char[] buffer, int index, int count)
        {
            if (_allowSynchronousIO)
            {
                _writer.Write(buffer, index, count);
            }
            else
            {
                CompleteSynchronously(_writer.WriteAsync(buffer, index, count));
            }
        }
    }
}
