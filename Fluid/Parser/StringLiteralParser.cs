using Parlot;
using Parlot.Fluent;
using Parlot.Rewriting;

namespace Fluid.Parser;

/// <summary>
/// Parses a single or double quoted string. Like Liquid, there are no escape sequences: the string
/// ends at the next matching quote and a backslash is a plain character.
/// </summary>
public sealed class StringLiteralParser : Parser<TextSpan>, ISeekable
{
    public bool CanSeek => true;

    public char[] ExpectedChars { get; } = ['\'', '"'];

    public bool SkipWhitespace => false;

    public override bool Parse(ParseContext context, ref ParseResult<TextSpan> result)
    {
        context.EnterParser(this);

        var cursor = context.Scanner.Cursor;
        var quote = cursor.Current;

        if (quote != '\'' && quote != '"')
        {
            context.ExitParser(this);
            return false;
        }

        var start = cursor.Offset;
        var buffer = context.Scanner.Buffer;
        var contentStart = start + 1;
        var closing = buffer.IndexOf(quote, contentStart);

        if (closing < 0)
        {
            context.ExitParser(this);
            return false;
        }

        cursor.Advance(closing + 1 - start);

        result.Set(start, closing + 1, new TextSpan(buffer, contentStart, closing - contentStart));

        context.ExitParser(this);
        return true;
    }
}
