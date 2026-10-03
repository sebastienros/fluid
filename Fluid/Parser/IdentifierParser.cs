using Parlot;
using Parlot.Fluent;
using Parlot.Rewriting;

namespace Fluid.Parser
{
    public sealed class IdentifierParser : Parser<TextSpan>, ISeekable
    {
        public const string StartChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_";
        private readonly bool _stripTrailingQuestion;

        public bool CanSeek => true;

        public char[] ExpectedChars { get; } = StartChars.ToCharArray();

        public bool SkipWhitespace => false;

        public IdentifierParser(bool stripTrailingQuestion = false)
        {
            _stripTrailingQuestion = stripTrailingQuestion;
        }

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

                context.ExitParser(this);
                return false;
            }

            // Read while it's an identifier part. and ensure we have at least a letter or it's a number

            cursor.Advance();

            var hasTrailingQuestion = false;

            while (!cursor.Eof)
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
                else if (_stripTrailingQuestion && current == '?' && !hasTrailingQuestion)
                {
                    // Allow one trailing '?' if the option is enabled
                    hasTrailingQuestion = true;
                    nonDigits++;
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
                end--;
                cursor.ResetPosition(lastDashPosition);
            }

            // Strip trailing '?' from the result if enabled and present.
            // This allows Ruby-style method names like 'empty?' to map to .NET properties like 'empty'.
            if (_stripTrailingQuestion && hasTrailingQuestion)
            {
                nonDigits--;
                end--;
            }

            if (nonDigits == 0)
            {
                // Invalid identifier, only digits
                cursor.ResetPosition(start);

                context.ExitParser(this);
                return false;
            }

            result.Set(start.Offset, end, new TextSpan(context.Scanner.Buffer, start.Offset, end - start.Offset));


            context.ExitParser(this);
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
