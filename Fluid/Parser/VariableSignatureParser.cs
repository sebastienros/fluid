using Parlot;
using Parlot.Fluent;
using Parlot.Rewriting;

namespace Fluid.Parser;

/// <summary>
/// A parser for variable signatures as defined in Shopify Liquid, i.e. one or more <c>\(?[\w\-\.\[\]]\)?</c>.
/// This parser is more permissive than IdentifierParser and allows:
/// - Digit-only identifiers (e.g., "123")
/// - Identifiers starting with digits (e.g., "2foo")
/// - Hyphens, dots and square brackets (e.g., "a-b", "a.b[0]"), which are part of the name
/// - Parentheses around any character (e.g., "(a)(b)")
/// 
/// This is used by capture, assign, increment, and decrement tags.
/// </summary>
public sealed class VariableSignatureParser : Parser<TextSpan>, ISeekable
{
    public const string StartChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_-.[](";

    public bool CanSeek => true;

    public char[] ExpectedChars { get; } = StartChars.ToCharArray();

    public bool SkipWhitespace => false;

    public override bool Parse(ParseContext context, ref ParseResult<TextSpan> result)
    {
        context.EnterParser(this);
        var cursor = context.Scanner.Cursor;

        var start = cursor.Position;
        var lastDashPosition = cursor.Position;
        var lastIsDash = false;

        while (!cursor.Eof)
        {
            var current = cursor.Current;

            if (current == '(' && IsSignatureChar(cursor.PeekNext()))
            {
                cursor.Advance();
                current = cursor.Current;
            }
            else if (!IsSignatureChar(current))
            {
                break;
            }

            lastIsDash = current == '-';

            if (lastIsDash)
            {
                lastDashPosition = cursor.Position;
            }

            cursor.Advance();

            if (cursor.Current == ')')
            {
                lastIsDash = false;
                cursor.Advance();
            }
        }

        var end = cursor.Offset;

        // Exclude the trailing '-' if it is next to an end tag
        // c.f. https://github.com/sebastienros/fluid/issues/347
        if (lastIsDash && !cursor.Eof && (cursor.Current == '%' || cursor.Current == '}'))
        {
            end--;
            cursor.ResetPosition(lastDashPosition);
        }

        if (end == start.Offset)
        {
            // Invalid identifier - no characters found
            cursor.ResetPosition(start);
            context.ExitParser(this);
            return false;
        }

        result.Set(start.Offset, end, new TextSpan(context.Scanner.Buffer, start.Offset, end - start.Offset));

        context.ExitParser(this);
        return true;
    }

    private static bool IsSignatureChar(char ch)
        => (ch >= 'a' && ch <= 'z') ||
           (ch >= 'A' && ch <= 'Z') ||
           (ch >= '0' && ch <= '9') ||
           ch is '_' or '-' or '.' or '[' or ']';
}
