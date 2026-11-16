using Parlot;
using Parlot.Fluent;
using Parlot.Rewriting;
using System.Runtime.CompilerServices;

namespace Fluid.Parser;

/// <summary>
/// Ensures enough stack space is left before invoking a parser that can recurse, so that a deeply nested template
/// fails with an <see cref="InsufficientExecutionStackException"/> instead of crashing the process with a stack overflow.
/// </summary>
/// <remarks>
/// The recursion depth limit of the <see cref="ParseContext"/> is deterministic but doesn't know the size of the
/// stack, which can be smaller than the limit requires.
/// </remarks>
internal sealed class StackGuardParser<T> : Parser<T>, ISeekable
{
    private readonly Parser<T> _parser;

    public StackGuardParser(Parser<T> parser)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
    }

    public bool CanSeek => _parser is ISeekable seekable && seekable.CanSeek;

    public char[] ExpectedChars => _parser is ISeekable seekable ? seekable.ExpectedChars : [];

    public bool SkipWhitespace => _parser is ISeekable seekable && seekable.SkipWhitespace;

    public override bool Parse(ParseContext context, ref ParseResult<T> result)
    {
        RuntimeHelpers.EnsureSufficientExecutionStack();

        return _parser.Parse(context, ref result);
    }
}
