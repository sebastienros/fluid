using System.Runtime.CompilerServices;

namespace Fluid
{
    /// <summary>
    /// Represents errors that occur during Liquid execution.
    /// </summary>
    public sealed class LiquidException : Exception
    {
        /// <inheritdoc />
        public LiquidException() : base() { }

        /// <inheritdoc />
        public LiquidException(string message) : base(message) { }

        /// <inheritdoc />
        public LiquidException(string message, Exception innerException) : base(message, innerException) { }

        public static void ThrowFilterArgumentsCount(string filter, int expected, FilterArguments arguments)
        {
            if (expected != arguments?.Count)
            {
                throw new LiquidException($"Wrong number of arguments for '{filter}' (given {arguments?.Count}, expected {expected})");
            }
        }

        public static void ThrowFilterArgumentsCount(string filter, int? min, int? max, FilterArguments arguments)
        {
            min ??= 0;
            max ??= int.MaxValue;

            if (arguments?.Count < min || arguments?.Count > max)
            {
                throw new LiquidException($"Wrong number of arguments for '{filter}' (given {arguments?.Count}, expected {min}..{max})");
            }
        }
    }
}
