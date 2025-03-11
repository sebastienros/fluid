namespace Fluid
{
    /// <summary>
    /// Represents errors that occur during template parsing.
    /// </summary>
    public sealed class ParseException : Exception
    {
        /// <inheritdoc />
        public ParseException() : base() { }

        /// <inheritdoc />
        public ParseException(string message) : base(message) { }

        /// <inheritdoc />
        public ParseException(string message, Exception innerException) : base(message, innerException) { }
    }
}
