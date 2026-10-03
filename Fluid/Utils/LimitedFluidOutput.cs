using System.Buffers;

namespace Fluid.Utils
{
    /// <summary>
    /// Enforces a cumulative character limit over another <see cref="IFluidOutput"/>.
    /// </summary>
    public sealed class LimitedFluidOutput : IFluidOutput
    {
        private readonly IFluidOutput _inner;
        private readonly int _maximum;
        private int _written;

        private LimitedFluidOutput(IFluidOutput inner, int maximum)
        {
            _inner = inner;
            _maximum = maximum;
        }

        public static IFluidOutput Create(IFluidOutput output, int maximum)
        {
            ArgumentNullException.ThrowIfNull(output);

            if (maximum <= 0)
            {
                return output;
            }

            if (output is LimitedFluidOutput limited && limited._maximum <= maximum)
            {
                return output;
            }

            return new LimitedFluidOutput(output, maximum);
        }

        public void Advance(int count)
        {
            EnsureAvailable(count);
            _inner.Advance(count);
            _written += count;
        }

        public Memory<char> GetMemory(int sizeHint = 0)
        {
            var remaining = GetRemaining(sizeHint);
            var memory = _inner.GetMemory(sizeHint);
            return memory.Length <= remaining ? memory : memory.Slice(0, remaining);
        }

        public Span<char> GetSpan(int sizeHint = 0)
        {
            var remaining = GetRemaining(sizeHint);
            var span = _inner.GetSpan(sizeHint);
            return span.Length <= remaining ? span : span.Slice(0, remaining);
        }

        public void Write(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            EnsureAvailable(value.Length);
            _inner.Write(value);
            _written += value.Length;
        }

        public void Write(char[] buffer, int index, int count)
        {
            ArgumentNullException.ThrowIfNull(buffer);
            EnsureAvailable(count);
            _inner.Write(buffer, index, count);
            _written += count;
        }

        public ValueTask FlushAsync() => _inner.FlushAsync();

        private int GetRemaining(int sizeHint)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(sizeHint);

            var remaining = _maximum - _written;
            if (remaining <= 0 || sizeHint > remaining)
            {
                ExceptionHelper.ThrowMaximumOutputSizeException(_maximum);
            }

            return remaining;
        }

        private void EnsureAvailable(int count)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(count);

            if (count > _maximum - _written)
            {
                ExceptionHelper.ThrowMaximumOutputSizeException(_maximum);
            }
        }
    }
}
