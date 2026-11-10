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
/// <para>
/// A line named <c>liquid</c> is the tag which follows this name: <c>liquid endcomment</c> closes the comment and
/// <c>liquid comment</c> opens a nested one.
/// </para>
/// <para>
/// A line named <c>raw</c> opens a raw block, whose lines are ignored up to the one named <c>endraw</c>, even the ones
/// named <c>comment</c> or <c>endcomment</c>. A raw block which isn't closed is a parse error.
/// </para>
/// <para>
/// The body stops before the name of the closing <c>endcomment</c> line, which is left to be read as a tag. If the
/// comment isn't closed, the body stops at the <c>%}</c> which ends the <c>{% liquid %}</c> tag, or at the end of the
/// template.
/// </para>
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
        var inRaw = false;

        while (position < end)
        {
            var name = ReadTagName(buffer, position, end, out var nameStart);

            if (inRaw)
            {
                if (name.SequenceEqual("endraw".AsSpan()))
                {
                    inRaw = false;
                }
            }
            else if (name.SequenceEqual("comment".AsSpan()))
            {
                depth++;
            }
            else if (name.SequenceEqual("endcomment".AsSpan()))
            {
                if (--depth == 0)
                {
                    position = nameStart;
                    break;
                }
            }
            else if (name.SequenceEqual("raw".AsSpan()))
            {
                inRaw = true;
            }

            // Skip to the next line, unless the {% liquid %} tag ends first.
            position = nameStart + name.Length;

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

        if (inRaw)
        {
            throw new Parlot.ParseException($"'{{% endraw %}}' was expected", cursor.Position);
        }

        result.Set(start, position, new TextSpan(buffer, start, position - start));

        context.ExitParser(this);
        return true;
    }

    /// <summary>
    /// Reads the tag name of the line at <paramref name="position"/>: the word after its leading whitespace, or the one
    /// after <c>liquid</c>, which reads what follows it as a tag. The name is empty when the line doesn't start with a word.
    /// </summary>
    private static ReadOnlySpan<char> ReadTagName(string buffer, int position, int end, out int nameStart)
    {
        while (true)
        {
            nameStart = position;

            while (nameStart < end && buffer[nameStart] is ' ' or '\t' or '\v' or '\f' or '\r')
            {
                nameStart++;
            }

            position = nameStart;

            while (position < end && IsWordChar(buffer[position]))
            {
                position++;
            }

            var name = buffer.AsSpan(nameStart, position - nameStart);

            if (!name.SequenceEqual("liquid".AsSpan()))
            {
                return name;
            }
        }
    }

    private static bool IsWordChar(char c) => c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '_';
}
