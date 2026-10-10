using System.Diagnostics.CodeAnalysis;
using System.Text.Encodings.Web;

namespace Fluid.Ast;

public abstract class Statement
{
    /// <summary>
    /// Gets the zero-based UTF-16 offset in the original template, or -1 when no source location is available.
    /// Locations are recorded when <see cref="FluidParserOptions.TrackStatementLocations"/> is enabled.
    /// </summary>
    [Experimental("FLUID001")]
    public int SourceOffset { get; internal set; } = -1;

    /// <summary>
    /// Gets the length in UTF-16 code units in the original template, or zero when no location is available.
    /// Block locations include their opening and closing tags and are not changed by whitespace trimming.
    /// </summary>
    [Experimental("FLUID001")]
    public int SourceLength { get; internal set; }

    public static readonly ValueTask<Completion> BreakCompletion = new(Completion.Break);
    public static readonly ValueTask<Completion> NormalCompletion = new(Completion.Normal);
    public static readonly ValueTask<Completion> ContinueCompletion = new(Completion.Continue);

    public static ValueTask<Completion> FromCompletion(Completion completion) => completion switch
    {
        Completion.Normal => NormalCompletion,
        Completion.Break => BreakCompletion,
        Completion.Continue => ContinueCompletion,
        _ => new(completion)
    };

    public static ValueTask<Completion> Break() => BreakCompletion;
    public static ValueTask<Completion> Normal() => NormalCompletion;
    public static ValueTask<Completion> Continue() => ContinueCompletion;

    /// <summary>
    /// Whether the statement doesn't output any text.
    /// In Liquid a block is suppressed only if it contains no output tokens, considering the entire control flow structure.
    /// </summary>
    public virtual bool IsWhitespaceOrCommentOnly => false;

    public abstract ValueTask<Completion> WriteToAsync(IFluidOutput output, TextEncoder encoder, TemplateContext context);

    protected internal virtual Statement Accept(AstVisitor visitor) => visitor.VisitOtherStatement(this);
}
