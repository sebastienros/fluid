using Parlot;
using Parlot.Fluent;

namespace Fluid.Parser;

/// <summary>
/// Reads the body of a <c>comment</c> block inside a <c>{% liquid %}</c> tag, where every line is a tag. Like Liquid, the
/// tag name of a line is the word after its leading whitespace: only a line named <c>endcomment</c> closes the comment,
/// and a line named <c>comment</c> opens a nested one, which needs its own <c>endcomment</c>. Other lines are ignored,
/// even when they contain <c>endcomment</c>, like <c>echo "endcomment"</c> or <c>sendcomment</c>.
/// </summary>
/// <remarks>
/// The body stops before the name of the closing <c>endcomment</c> line, which is left to be read as a tag. If the
/// comment isn't closed, the body stops at the <c>%}</c> which ends the <c>{% liquid %}</c> tag, or at the end of the
/// template.
/// </remarks>
internal sealed class LiquidTagCommentBodyParser : Parser<TextSpan>
{
    public override bool Parse(ParseContext context, ref ParseResult<TextSpan> result)
    {
        context.EnterParser(this);

        var cursor = context.Scanner.Cursor;
        var buffer = context.Scanner.Buffer;
        var start = cursor.Offset;
        var end = buffer.Length;
        var position = start;
        var depth = 1;

        while (position < end)
        {
            var nameStart = position;

            while (nameStart < end && buffer[nameStart] is ' ' or '\t' or '\v' or '\f' or '\r')
            {
                nameStart++;
            }

            var nameEnd = nameStart;

            while (nameEnd < end && IsWordChar(buffer[nameEnd]))
            {
                nameEnd++;
            }

            var name = buffer.AsSpan(nameStart, nameEnd - nameStart);

            if (name.SequenceEqual("comment".AsSpan()))
            {
                depth++;
            }
            else if (name.SequenceEqual("endcomment".AsSpan()) && --depth == 0)
            {
                position = nameStart;
                break;
            }

            // Skip to the next line, unless the {% liquid %} tag ends first.
            position = nameEnd;

            while (position < end && buffer[position] != '\n' && !(buffer[position] == '%' && position + 1 < end && buffer[position + 1] == '}'))
            {
                position++;
            }

            if (position == end || buffer[position] != '\n')
            {
                break;
            }

            position++;
        }

        cursor.Advance(position - start);
        result.Set(start, position, new TextSpan(buffer, start, position - start));

        context.ExitParser(this);
        return true;
    }

    private static bool IsWordChar(char c) => c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '_';
}
