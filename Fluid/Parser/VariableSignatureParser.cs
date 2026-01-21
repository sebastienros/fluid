using Parlot;
using Parlot.Fluent;
using Parlot.Rewriting;

namespace Fluid.Parser
{
    /// <summary>
    /// A parser for variable signatures as defined in Shopify Liquid.
    /// This parser is more permissive than IdentifierParser and allows:
    /// - Digit-only identifiers (e.g., "123")
    /// - Identifiers starting with digits (e.g., "2foo")
    /// - Regular identifiers with letters, digits, underscores, and hyphens
    /// 
    /// This is used by capture, assign, increment, and decrement tags.
    /// </summary>
    public sealed class VariableSignatureParser : Parser<TextSpan>, ISeekable
    {
        public const string StartChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_";

        public bool CanSeek => true;

        public char[] ExpectedChars { get; } = StartChars.ToCharArray();

        public bool SkipWhitespace => false;

        public override bool Parse(ParseContext context, ref ParseResult<TextSpan> result)
        {
            context.EnterParser(this);
            var cursor = context.Scanner.Cursor;

            var current = cursor.Current;

            var start = cursor.Position;
            var lastDashPosition = cursor.Position;
            var lastIsDash = false;
            var hasAnyChar = false;

            // Must start with a letter, digit, or underscore
            if (IsValidStartChar(current))
            {
                hasAnyChar = true;
            }
            else
            {
                // Doesn't start with a valid character
                context.ExitParser(this);
                return false;
            }

            cursor.Advance();

            // Read while it's a valid identifier part
            while (!cursor.Eof)
            {
                current = cursor.Current;

                if (IsValidStartChar(current))
                {
                    lastIsDash = false;
                }
                else if (current == '-')
                {
                    lastDashPosition = cursor.Position;
                    lastIsDash = true;
                }
                else
                {
                    break;
                }

                cursor.Advance();
            }

            var end = cursor.Offset;

            // Exclude the trailing '-' if it is next to an end tag
            // c.f. https://github.com/sebastienros/fluid/issues/347
            current = cursor.Current;

            if (lastIsDash && !cursor.Eof && (current == '%' || current == '}'))
            {
                end--;
                cursor.ResetPosition(lastDashPosition);
            }

            if (!hasAnyChar)
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

        private static bool IsValidStartChar(char ch)
            => (ch >= 'a' && ch <= 'z') ||
               (ch >= 'A' && ch <= 'Z') ||
               (ch >= '0' && ch <= '9') ||
               (ch == '_');
    }
}
