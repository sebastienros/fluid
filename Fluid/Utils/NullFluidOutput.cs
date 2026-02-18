using System.Buffers;

namespace Fluid.Utils
{
    internal sealed class NullFluidOutput : IFluidOutput
    {
        public static readonly NullFluidOutput Instance = new();

        private NullFluidOutput() { }

        public void Advance(int count) { }

        public Memory<char> GetMemory(int sizeHint = 0) => Memory<char>.Empty;

        public Span<char> GetSpan(int sizeHint = 0) => Span<char>.Empty;

        public void Write(string value) { }

        public void Write(char[] buffer, int index, int count) { }

        public ValueTask FlushAsync() => default;
    }
}
