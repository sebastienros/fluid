using Fluid.Ast;
using Parlot;
using Parlot.Fluent;
using Parlot.Rewriting;

namespace Fluid.Parser
{
    public struct ForModifier
    {
        public bool IsReversed;
        public bool IsLimit;
        public bool IsOffset;
        public Expression Value;
    }

    public readonly struct TagResult
    {
        public static readonly TagResult TagOpen = new TagResult(true, false);
        public static readonly TagResult TagOpenTrim = new TagResult(true, true);
        public static readonly TagResult TagClose = new TagResult(false, false);
        public static readonly TagResult TagCloseTrim = new TagResult(false, true);

        public TagResult(bool open, bool trim)
        {
            Open = open;
            Trim = trim;
        }

        public readonly bool Open;
        public readonly bool Trim;
    }

    public static class TagParsers
    {
        public static Parser<TagResult> TagStart(bool skipWhiteSpace = false) => new TagStartParser(skipWhiteSpace);
        public static Parser<TagResult> TagEnd(bool skipWhiteSpace = false) => new TagEndParser(skipWhiteSpace);
        public static Parser<TagResult> OutputTagStart(bool skipWhiteSpace = false) => new OutputTagStartParser(skipWhiteSpace);
        public static Parser<TagResult> OutputTagEnd(bool skipWhiteSpace = false) => new OutputTagEndParser(skipWhiteSpace);

        private sealed class TagStartParser : Parser<TagResult>
        {
            private readonly bool _skipWhiteSpace;
            public TagStartParser(bool skipWhiteSpace = false)
            {
                _skipWhiteSpace = skipWhiteSpace;
            }

            public override bool Parse(ParseContext context, ref ParseResult<TagResult> result)
            {
                context.EnterParser(this);

                if (_skipWhiteSpace)
                {
                    context.SkipWhiteSpace();
                }

                var start = context.Scanner.Cursor.Position;

                var p = (FluidParseContext)context;

                if (p.InsideLiquidTag)
                {
                    result.Set(start.Offset, context.Scanner.Cursor.Offset, TagResult.TagOpen);

                    context.ExitParser(this);
                    return true;
                }

                if (context.Scanner.ReadChar('{') && context.Scanner.ReadChar('%'))
                {
                    var trim = context.Scanner.ReadChar('-');

                    if (p.PreviousTextSpanStatement != null)
                    {
                        if (trim)
                        {
                            p.PreviousTextSpanStatement.StripRight = true;
                        }

                        p.PreviousTextSpanStatement.NextIsTag = true;

                        p.PreviousTextSpanStatement = null;
                    }

                    result.Set(start.Offset, context.Scanner.Cursor.Offset, trim ? TagResult.TagOpenTrim : TagResult.TagOpen);

                    context.ExitParser(this);
                    return true;
                }
                else
                {
                    context.Scanner.Cursor.ResetPosition(start);

                    context.ExitParser(this);
                    return false;
                }
            }
        }

        /// <summary>
        /// Search for `%}`, `-%}` or `-}` to close a tag.
        /// Also, if the tag is inside a `liquid` tag, it will only look for a new line to close the tag.
        /// </summary>
        private sealed class TagEndParser : Parser<TagResult>, ISeekable
        {
            private readonly bool _skipWhiteSpace;

            public bool CanSeek { get; set; } = true;
            public bool SkipWhitespace { get; set; } = false;
            public char[] ExpectedChars { get; set; } = ['\r', '\n', '}', '-', '%', ' ', '\t'];

            public TagEndParser(bool skipWhiteSpace = false)
            {
                _skipWhiteSpace = skipWhiteSpace;
            }

            public override bool Parse(ParseContext context, ref ParseResult<TagResult> result)
            {
                var p = (FluidParseContext)context;

                var newLineIsPresent = false;

                if (_skipWhiteSpace)
                {
                    if (p.InsideLiquidTag)
                    {
                        var cursor = context.Scanner.Cursor;

                        while (Character.IsWhiteSpace(cursor.Current))
                        {
                            cursor.Advance();
                        }

                        if (Character.IsNewLine(cursor.Current))
                        {
                            newLineIsPresent = true;
                            while (Character.IsNewLine(cursor.Current))
                            {
                                cursor.Advance();
                            }
                        }
                    }
                    else
                    {
                        context.SkipWhiteSpace();
                    }
                }

                var start = context.Scanner.Cursor.Position;
                bool trim;

                if (p.InsideLiquidTag)
                {
                    if (newLineIsPresent)
                    {
                        result.Set(start.Offset, context.Scanner.Cursor.Offset, TagResult.TagClose);
                        return true;
                    }
                    else
                    {
                        trim = context.Scanner.ReadChar('-');

                        if (context.Scanner.ReadChar('%') && context.Scanner.ReadChar('}'))
                        {
                            p.StripNextTextSpanStatement = trim;
                            p.PreviousTextSpanStatement = null;
                            p.PreviousIsTag = true;
                            p.PreviousIsOutput = false;

                            context.Scanner.Cursor.ResetPosition(start);

                            result.Set(start.Offset, start.Offset, TagResult.TagClose);
                            return true;
                        }

                        context.Scanner.Cursor.ResetPosition(start);
                        return false;
                    }
                }

                trim = context.Scanner.ReadChar('-');

                if (context.Scanner.ReadChar('%') && context.Scanner.ReadChar('}'))
                {
                    p.StripNextTextSpanStatement = trim;
                    p.PreviousTextSpanStatement = null;
                    p.PreviousIsTag = true;
                    p.PreviousIsOutput = false;

                    result.Set(start.Offset, context.Scanner.Cursor.Offset, trim ? TagResult.TagCloseTrim : TagResult.TagClose);
                    return true;
                }
                else
                {
                    context.Scanner.Cursor.ResetPosition(start);
                    return false;
                }
            }
        }

        private sealed class OutputTagStartParser : Parser<TagResult>, ISeekable
        {
            public OutputTagStartParser(bool skipWhiteSpace = false)
            {
                SkipWhitespace = skipWhiteSpace;
            }

            public bool CanSeek => true;

            public char[] ExpectedChars { get; set; } = ['{'];

            public bool SkipWhitespace { get; }

            public override bool Parse(ParseContext context, ref ParseResult<TagResult> result)
            {
                if (SkipWhitespace)
                {
                    context.SkipWhiteSpace();
                }

                var start = context.Scanner.Cursor.Position;

                if (context.Scanner.ReadChar('{') && context.Scanner.ReadChar('{'))
                {
                    var trim = context.Scanner.ReadChar('-');

                    var p = (FluidParseContext)context;

                    if (p.PreviousTextSpanStatement != null)
                    {
                        if (trim)
                        {
                            p.PreviousTextSpanStatement.StripRight = true;
                        }

                        p.PreviousTextSpanStatement.NextIsOutput = true;

                        p.PreviousTextSpanStatement = null;
                    }


                    result.Set(start.Offset, context.Scanner.Cursor.Offset, trim ? TagResult.TagOpenTrim : TagResult.TagOpen);
                    return true;
                }
                else
                {
                    context.Scanner.Cursor.ResetPosition(start);
                    return false;
                }
            }
        }

        private sealed class OutputTagEndParser : Parser<TagResult>, ISeekable
        {
            public OutputTagEndParser(bool skipWhiteSpace = false)
            {
                SkipWhitespace = skipWhiteSpace;
            }

            public bool CanSeek => true;

            public char[] ExpectedChars { get; set; } = ['-', '}'];

            public bool SkipWhitespace { get; }

            public override bool Parse(ParseContext context, ref ParseResult<TagResult> result)
            {
                context.EnterParser(this);

                if (SkipWhitespace)
                {
                    context.SkipWhiteSpace();
                }

                var start = context.Scanner.Cursor.Position;

                var trim = context.Scanner.ReadChar('-');

                if (context.Scanner.ReadChar('}') && context.Scanner.ReadChar('}'))
                {
                    var p = (FluidParseContext)context;

                    p.StripNextTextSpanStatement = trim;
                    p.PreviousTextSpanStatement = null;
                    p.PreviousIsTag = false;
                    p.PreviousIsOutput = true;

                    result.Set(start.Offset, context.Scanner.Cursor.Offset, trim ? TagResult.TagCloseTrim : TagResult.TagClose);


                    context.ExitParser(this);
                    return true;
                }
                else
                {
                    context.Scanner.Cursor.ResetPosition(start);

                    context.ExitParser(this);
                    return false;
                }
            }
        }
    }
}
