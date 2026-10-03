using System;

namespace Fluid
{
    /// <summary>
    /// Represents errors that occur during Fluid template parsing or rendering.
    /// </summary>
    public class FluidException : Exception
    {
        public FluidException() { }

        /// <inheritdoc/>
        public FluidException(string message) : base(message) { }

        /// <inheritdoc/>
        public FluidException(string message, Exception innerException) : base(message, innerException) { }
    }
}
