using Parlot;

namespace Fluid;

/// <summary>
/// Represents errors that occur during template parsing.
/// </summary>
public sealed class ParseException : FluidException
{
    /// <summary>
    /// Gets the full template source, or <see langword="null"/> when it is unavailable.
    /// </summary>
    public string TemplateSource { get; }

    /// <summary>
    /// Gets the error position, or <see langword="null"/> when it is unavailable.
    /// The offset is zero-based; the line and column are one-based. Lines are delimited by LF.
    /// </summary>
    public TextPosition? Position { get; }

    /// <inheritdoc />
    public ParseException() : base() { }

    /// <inheritdoc />
    public ParseException(string message) : base(message) { }

    /// <inheritdoc />
    public ParseException(string message, Exception innerException) : base(message, innerException) { }

    /// <summary>
    /// Creates a parsing exception with template source and position information.
    /// </summary>
    public ParseException(string message, string templateSource, TextPosition? position)
        : this(message, templateSource, position, null) { }

    /// <summary>
    /// Creates a parsing exception with template source, position information, and an inner exception.
    /// </summary>
    public ParseException(string message, string templateSource, TextPosition? position, Exception innerException)
        : base(message, innerException)
    {
        TemplateSource = templateSource;
        Position = position;
    }
}
