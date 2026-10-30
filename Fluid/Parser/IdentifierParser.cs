using Parlot;
using Parlot.Fluent;
using static Parlot.Fluent.Parsers;

namespace Fluid.Parser;

/// <summary>
/// Parses identifiers: letters, digits, <c>_</c> and <c>-</c>, with at least a char that is not a digit.
/// A trailing <c>-</c> next to an end tag, as in <c>{{ a-}}</c>, is not part of the identifier but a trim marker.
/// c.f. https://github.com/sebastienros/fluid/issues/347
/// </summary>
public static class IdentifierParser
{
    public const string StartChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_";

    /// <summary>
    /// Creates a parser of identifiers. When <paramref name="stripTrailingQuestion"/> is set, an identifier can end
    /// with a <c>?</c>, which is read but not returned. This allows Ruby-style method names like <c>empty?</c> to map
    /// to .NET properties like <c>empty</c>.
    /// </summary>
    public static Parser<TextSpan> Create(bool stripTrailingQuestion = false)
    {
        if (!stripTrailingQuestion)
        {
            return Literals.Scan(static span => Scan(span, trailingQuestion: false), StartChars.AsSpan());
        }

        return Literals.Scan(static span => Scan(span, trailingQuestion: true), StartChars.AsSpan())
            .Then(static x => x.Span[x.Length - 1] == '?' ? new TextSpan(x.Buffer, x.Offset, x.Length - 1) : x);
    }

    /// <summary>
    /// Returns the length of the identifier at the start of <paramref name="span"/>, with its trailing <c>?</c> when
    /// <paramref name="trailingQuestion"/> is set, <c>0</c> when there is none, or a negative value when more text
    /// could make one (see <see cref="SpanScanner"/>).
    /// </summary>
    public static int Scan(ReadOnlySpan<char> span, bool trailingQuestion)
    {
        if (span.IsEmpty)
        {
            return -1;
        }

        var current = span[0];
        var nonDigits = 0;

        if (IsNonDigitStart(current))
        {
            nonDigits++;
        }
        else if (!char.IsDigit(current))
        {
            // Doesn't start with a letter or a digit
            return 0;
        }

        // Read while it's an identifier part, and ensure we have at least a letter or it's a number

        var size = 1;
        var lastIsDash = false;

        while (size < span.Length)
        {
            current = span[size];

            if (IsNonDigitStart(current))
            {
                nonDigits++;
                lastIsDash = false;
            }
            else if (current == '-')
            {
                nonDigits++;
                lastIsDash = true;
            }
            else if (char.IsDigit(current))
            {
                lastIsDash = false;
            }
            else if (trailingQuestion && current == '?')
            {
                // A '?' ends the identifier, like in Shopify's lexer: [a-zA-Z_][\w-]*\??
                return nonDigits > 0 ? size + 1 : 0;
            }
            else
            {
                // Exclude the trailing '-' if it is next to an end tag
                if (lastIsDash && current is '%' or '}')
                {
                    nonDigits--;
                    size--;
                }

                // Invalid identifier when it's only digits
                return nonDigits > 0 ? size : 0;
            }

            size++;
        }

        // The identifier reaches the end of the text, which more text could continue
        return nonDigits > 0 ? size : -1;
    }

    private static bool IsNonDigitStart(char ch)
        =>
           (ch >= 'a' && ch <= 'z') ||
           (ch >= 'A' && ch <= 'Z') ||
            (ch == '_')
        ;
}
