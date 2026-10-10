using System.Diagnostics.CodeAnalysis;

namespace Fluid;

/// <summary>
/// Parser options. Instances are immutable once created, so set the options with an object initializer.
/// </summary>
public sealed class FluidParserOptions
{
    /// <summary>
    /// Gets whether original source offsets and lengths are recorded on statements. Default is <c>false</c>.
    /// </summary>
    [Experimental("FLUID001")]
    public bool TrackStatementLocations { get; init; }

    /// <summary>
    /// Gets whether functions are allowed in templates. Default is <c>false</c>.
    /// </summary>
    public bool AllowFunctions { get; init; }

    /// <summary>
    /// Gets whether parentheses are allowed in templates. Default is <c>false</c>.
    /// </summary>
    public bool AllowParentheses { get; init; }

    /// <summary>
    /// Gets whether the inline liquid tag is allowed in templates. Default is <c>false</c>.
    /// </summary>
    public bool AllowLiquidTag { get; init; }

    /// <summary>
    /// Gets whether identifiers can end with a question mark (`?`), which will be stripped during parsing. Default is <c>false</c>.
    /// </summary>
    public bool AllowTrailingQuestionMark { get; init; }
}
