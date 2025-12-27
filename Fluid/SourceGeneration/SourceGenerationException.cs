namespace Fluid.SourceGeneration
{
    public sealed class SourceGenerationException : Exception
    {
        public SourceGenerationException(string message) : base(message)
        {
        }

        public SourceGenerationException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
