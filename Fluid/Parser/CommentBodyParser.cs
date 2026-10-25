using Parlot;
using Parlot.Fluent;

namespace Fluid.Parser;

/// <summary>
/// Parses the body of a <c>{% comment %}</c> block, up to (and excluding) its matching <c>{% endcomment %}</c> tag.
/// Like Shopify Liquid, the body is split into tokens the same way as a template: tags inside it don't need to be
/// valid, but nested <c>comment</c> and <c>raw</c> blocks must be balanced. A tag that the tokenizer merges into
/// a preceding incomplete tag or output (e.g. <c>{{ {% endcomment %}</c>) doesn't count.
/// Inside a <c>{% liquid %}</c> tag, the parser given to the constructor is used instead.
/// </summary>
internal sealed class CommentBodyParser : Parser<TextSpan>
{
    private readonly Parser<TextSpan> _liquidTagBody;

    public CommentBodyParser(Parser<TextSpan> liquidTagBody)
    {
        _liquidTagBody = liquidTagBody;
    }

    public override bool Parse(ParseContext context, ref ParseResult<TextSpan> result)
    {
        if (((FluidParseContext)context).LiquidTagDepth > 0)
        {
            return _liquidTagBody.Parse(context, ref result);
        }

        context.EnterParser(this);

        var cursor = context.Scanner.Cursor;
        var buffer = context.Scanner.Buffer;
        var start = cursor.Offset;
        var offset = start;
        var depth = 1;
        var end = buffer.Length;

        while (offset < buffer.Length)
        {
            var tokenStart = offset;
            var token = NextToken(buffer, ref offset);

            if (token != TokenKind.Tag)
            {
                continue;
            }

            var name = GetTagName(buffer, tokenStart, offset);

            if (name.SequenceEqual("raw".AsSpan()))
            {
                if (!SkipRawBody(buffer, ref offset))
                {
                    break;
                }
            }
            else if (name.SequenceEqual("comment".AsSpan()))
            {
                depth++;
            }
            else if (name.SequenceEqual("endcomment".AsSpan()) && --depth == 0)
            {
                end = tokenStart;
                break;
            }
        }

        // When the block is never closed, everything is consumed and the endcomment tag parser reports the error.
        cursor.Advance(end - start);

        result.Set(start, end, new TextSpan(buffer, start, end - start));

        context.ExitParser(this);
        return true;
    }

    private enum TokenKind { Text, Tag, Other }

    // Same rules as the Shopify Liquid tokenizer.
    private static TokenKind NextToken(string buffer, ref int offset)
    {
        if (buffer[offset] == '{' && offset + 1 < buffer.Length)
        {
            if (buffer[offset + 1] == '%')
            {
                var close = buffer.IndexOf("%}", offset + 2, StringComparison.Ordinal);

                if (close == -1)
                {
                    // An unclosed '{%' is a token on its own.
                    offset += 2;
                    return TokenKind.Other;
                }

                offset = close + 2;
                return TokenKind.Tag;
            }

            if (buffer[offset + 1] == '{')
            {
                offset = EndOfOutputToken(buffer, offset);
                return TokenKind.Other;
            }
        }

        var next = offset + 1;

        while (next < buffer.Length && !(buffer[next - 1] == '{' && (buffer[next] == '{' || buffer[next] == '%')))
        {
            next++;
        }

        offset = next < buffer.Length ? next - 1 : buffer.Length;
        return TokenKind.Text;
    }

    // An output token ends at the first '}' or '}}'. If a '{%' comes first, the token extends to the next '%}'.
    private static int EndOfOutputToken(string buffer, int start)
    {
        var i = start + 2;

        while (i < buffer.Length)
        {
            var c = buffer[i];

            if (c == '}')
            {
                return i + 1 < buffer.Length && buffer[i + 1] == '}' ? i + 2 : i + 1;
            }

            if (c == '{' && i + 1 < buffer.Length && buffer[i + 1] == '%')
            {
                var close = buffer.IndexOf("%}", i + 2, StringComparison.Ordinal);
                return close == -1 ? i + 2 : close + 2;
            }

            i++;
        }

        return buffer.Length;
    }

    // The name of a tag token, as in '{%- name ... -%}'. Empty if it doesn't start with a word.
    private static ReadOnlySpan<char> GetTagName(string buffer, int start, int end)
    {
        var i = start + 2;

        if (i < end && buffer[i] == '-')
        {
            i++;
        }

        while (i < end && IsWhiteSpace(buffer[i]))
        {
            i++;
        }

        var nameStart = i;

        while (i < end && IsWordChar(buffer[i]))
        {
            i++;
        }

        return buffer.AsSpan(nameStart, i - nameStart);
    }

    // Skips tokens until one ends with an '{% endraw %}' tag. Returns false if there is none.
    private static bool SkipRawBody(string buffer, ref int offset)
    {
        while (offset < buffer.Length)
        {
            var tokenStart = offset;

            if (NextToken(buffer, ref offset) == TokenKind.Text || !buffer.AsSpan(tokenStart, offset - tokenStart).EndsWith("%}".AsSpan()))
            {
                continue;
            }

            // Like Ruby's /\A(.*)\{%-?\s*(\w+)/, the name comes from the last '{%' of the token that is followed by a word.
            var tagStart = offset;

            while (tagStart > tokenStart && (tagStart = buffer.LastIndexOf("{%", tagStart - 1, tagStart - tokenStart, StringComparison.Ordinal)) != -1)
            {
                var name = GetTagName(buffer, tagStart, offset);

                if (name.Length > 0)
                {
                    if (name.SequenceEqual("endraw".AsSpan()))
                    {
                        return true;
                    }

                    break;
                }
            }
        }

        return false;
    }

    private static bool IsWhiteSpace(char c) => c is ' ' or '\t' or '\n' or '\v' or '\f' or '\r';

    private static bool IsWordChar(char c) => c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '_';
}
