using Parlot;
using Parlot.Fluent;
using Parlot.Rewriting;

namespace Fluid.Parser;

/// <summary>
/// Parses a number literal too large for a decimal, like <c>-?\d{29,}(\.\d+)?</c>: an optional minus sign, the digits,
/// and an optional point followed by digits. Nothing else is taken, so the <c>-</c> of <c>-}}</c> and the <c>..</c> of a
/// range are left to the next parser.
/// </summary>
internal sealed class BigNumberLiteralParser : Parser<TextSpan>, ISeekable
{
    // The largest decimal, 79228162514264337593543950335, has 29 digits: a number with fewer digits before the point is a
    // decimal, which Terms.Decimal parses first.
    private const int MinIntegerDigits = 29;

    public bool CanSeek => true;

    public char[] ExpectedChars { get; } = ['-', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9'];

    public bool SkipWhitespace => false;

    public override bool Parse(ParseContext context, ref ParseResult<TextSpan> result)
    {
        context.EnterParser(this);

        var cursor = context.Scanner.Cursor;
        var buffer = context.Scanner.Buffer;
        var start = cursor.Offset;
        var i = start;

        if (i < buffer.Length && buffer[i] == '-')
        {
            i++;
        }

        var integerStart = i;
        i = SkipDigits(buffer, i);

        if (i - integerStart < MinIntegerDigits)
        {
            context.ExitParser(this);
            return false;
        }

        // A point is part of the number only when a digit follows it: (99999999999999999999999999999..1) is a range.
        if (i + 1 < buffer.Length && buffer[i] == '.' && IsDigit(buffer[i + 1]))
        {
            i = SkipDigits(buffer, i + 1);
        }

        cursor.Advance(i - start);

        result.Set(start, i, new TextSpan(buffer, start, i - start));

        context.ExitParser(this);
        return true;
    }

    private static int SkipDigits(string buffer, int i)
    {
        while (i < buffer.Length && IsDigit(buffer[i]))
        {
            i++;
        }

        return i;
    }

    private static bool IsDigit(char c) => c is >= '0' and <= '9';
}
