using Parlot;
using Parlot.Fluent;
using static Parlot.Fluent.Parsers;

namespace Fluid.Parser;

/// <summary>
/// Parses variable signatures as defined in Shopify Liquid, i.e. one or more <c>\(?[\w\-\.\[\]]\)?</c>.
/// This parser is more permissive than IdentifierParser and allows:
/// - Digit-only identifiers (e.g., "123")
/// - Identifiers starting with digits (e.g., "2foo")
/// - Hyphens, dots and square brackets (e.g., "a-b", "a.b[0]"), which are part of the name
/// - Parentheses around any character (e.g., "(a)(b)")
///
/// This is used by capture, assign, increment, and decrement tags.
/// </summary>
public static class VariableSignatureParser
{
    public const string StartChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_-.[](";

    public static Parser<TextSpan> Create() => Literals.Scan(static span => Scan(span), StartChars.AsSpan());

    /// <summary>
    /// Returns the length of the variable signature at the start of <paramref name="span"/>, <c>0</c> when there is
    /// none, or a negative value when more text could tell (see <see cref="SpanScanner"/>).
    /// </summary>
    public static int Scan(ReadOnlySpan<char> span)
    {
        var size = 0;
        var lastIsDash = false;

        while (size < span.Length)
        {
            var current = span[size];

            // A '(' is part of the signature when a signature char follows it
            if (current == '(')
            {
                if (size + 1 == span.Length)
                {
                    return -1;
                }

                if (!IsSignatureChar(span[size + 1]))
                {
                    break;
                }

                size++;
                current = span[size];
            }
            else if (!IsSignatureChar(current))
            {
                break;
            }

            lastIsDash = current == '-';

            size++;

            if (size < span.Length && span[size] == ')')
            {
                lastIsDash = false;
                size++;
            }
        }

        // Exclude the trailing '-' if it is next to an end tag
        // c.f. https://github.com/sebastienros/fluid/issues/347
        if (lastIsDash && size < span.Length && span[size] is '%' or '}')
        {
            size--;
        }

        return size;
    }

    private static bool IsSignatureChar(char ch)
        => (ch >= 'a' && ch <= 'z') ||
           (ch >= 'A' && ch <= 'Z') ||
           (ch >= '0' && ch <= '9') ||
           ch is '_' or '-' or '.' or '[' or ']';
}
