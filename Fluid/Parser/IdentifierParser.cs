using Parlot;
using Parlot.Fluent;

namespace Fluid.Parser
{
    public sealed class IdentifierParser : Parser<TextSpan>
    {
        public override bool Parse(ParseContext context, ref ParseResult<TextSpan> result)
        {
            context.EnterParser(this);
            var cursor = context.Scanner.Cursor;

            var current = cursor.Current;

            var nonDigits = 0;
            var lastIsDash = false;

            var start = cursor.Position;
            var lastDashPosition = cursor.Position;

            if (IsNonDigitStart(current))
            {
                nonDigits++;
            }
            else if (char.IsDigit(current))
            {
            }
            else
            {
                // Doesn't start with a letter or a digit
                return false;
            }

            // Read while it's an identifier part. and ensure we have at least a letter or it's a number

            cursor.Advance();

            while (!context.Scanner.Cursor.Eof)
            {
                current = cursor.Current;

                if (IsNonDigitStart(current))
                {
                    nonDigits++;
                    lastIsDash = false;
                }
                else if (current == '-')
                {
                    lastDashPosition = cursor.Position;
                    nonDigits++;
                    lastIsDash = true;
                }
                else if (char.IsDigit(current))
                {
                    lastIsDash = false;
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
                nonDigits--;
                end = end - 1;
                cursor.ResetPosition(lastDashPosition);
            }

            if (nonDigits == 0)
            {
                // Invalid identifier, only digits
                cursor.ResetPosition(start);
                return false;
            }

            result.Set(start.Offset, end, new TextSpan(context.Scanner.Buffer, start.Offset, end - start.Offset));
            return true;
        }

        private static bool IsNonDigitStart(char ch)
            =>
               (ch >= 'a' && ch <= 'z') ||
               (ch >= 'A' && ch <= 'Z') ||
                (ch == '_')
            ;
    }
}