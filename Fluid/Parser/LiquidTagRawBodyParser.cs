using Parlot;
using Parlot.Fluent;

namespace Fluid.Parser;

/// <summary>
/// Reads the body of a <c>raw</c> block inside a <c>{% liquid %}</c> tag, from the end of the <c>raw</c> line. Like
/// Liquid, the markup after <c>raw</c> is ignored and the body starts with the new line which ends the <c>raw</c> line,
/// so it starts with a new line even when it is empty.
/// </summary>
/// <remarks>
/// <para>
/// The body ends with the line named <c>endraw</c>, the word after its leading whitespace or after <c>liquid</c> (see
/// <see cref="LiquidTagCommentBodyParser"/>), like <c>  endraw</c> or <c>liquid endraw</c>: other lines, even
/// <c>xendraw</c> or <c>echo endraw</c>, are part of the body. The leading whitespace of the <c>endraw</c> line isn't
/// part of the body, but the text before <c>endraw</c> on a <c>liquid</c> line is.
/// </para>
/// <para>
/// The parser stops before the name of the <c>endraw</c> line, which is left to be read as a tag. It fails if the
/// <c>{% liquid %}</c> tag, which ends at the first <c>%}</c>, or the template ends first.
/// </para>
/// </remarks>
internal sealed class LiquidTagRawBodyParser : Parser<TextSpan>
{
    public override bool Parse(ParseContext context, ref ParseResult<TextSpan> result)
    {
        context.EnterParser(this);

        var cursor = context.Scanner.Cursor;
        var buffer = context.Scanner.Buffer;
        var start = cursor.Offset;
        var end = buffer.Length;

        // Skip the markup of the raw line: the body starts with its new line.
        var position = SkipToLineEnd(buffer, start, end);
        var bodyStart = position;

        while (position < end && buffer[position] == '\n')
        {
            var lineStart = position + 1;
            var name = LiquidTagCommentBodyParser.ReadTagName(buffer, lineStart, end, out var nameStart);

            if (name.SequenceEqual("endraw".AsSpan()))
            {
                var bodyEnd = IsLineWhiteSpace(buffer, lineStart, nameStart) ? lineStart : nameStart;

                cursor.Advance(nameStart - start);
                result.Set(bodyStart, bodyEnd, new TextSpan(buffer, bodyStart, bodyEnd - bodyStart));

                context.ExitParser(this);
                return true;
            }

            position = SkipToLineEnd(buffer, nameStart + name.Length, end);
        }

        context.ExitParser(this);
        return false;
    }

    /// <summary>
    /// Returns the position of the new line which ends the line, or of the <c>%}</c> which ends the <c>{% liquid %}</c>
    /// tag, or the end of the template.
    /// </summary>
    private static int SkipToLineEnd(string buffer, int position, int end)
    {
        while (position < end && buffer[position] != '\n' && !(buffer[position] == '%' && position + 1 < end && buffer[position + 1] == '}'))
        {
            position++;
        }

        return position;
    }

    private static bool IsLineWhiteSpace(string buffer, int start, int end)
    {
        for (var i = start; i < end; i++)
        {
            if (buffer[i] is not (' ' or '\t' or '\v' or '\f' or '\r'))
            {
                return false;
            }
        }

        return true;
    }
}
