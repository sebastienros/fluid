using System.Buffers;

namespace Fluid
{
    /// <summary>
    /// Represents a buffered, flushable output target for template rendering.
    /// </summary>
    /// <remarks>
    /// This abstraction is designed for high-throughput rendering while retaining bounded memory usage.
    /// Implementations typically buffer written content and flush to an underlying destination when a threshold is reached.
    /// </remarks>
    public interface IFluidOutput : IBufferWriter<char>
    {
        /// <summary>
        /// Writes a string to the output.
        /// </summary>
        void Write(string value);

        /// <summary>
        /// Writes a range of characters to the output.
        /// </summary>
        void Write(char[] buffer, int index, int count);

        /// <summary>
        /// Flushes any buffered content asynchronously.
        /// </summary>
        ValueTask FlushAsync();
    }
}
