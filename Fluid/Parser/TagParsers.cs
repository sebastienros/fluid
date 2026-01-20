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

    public struct TableRowModifier
    {
        public bool IsCols;
        public bool IsLimit;
        public bool IsOffset;
        public Expression Value;
    }

    public readonly struct TagResult
    {
        public static readonly TagResult TagOpen = new(true, false);
        public static readonly TagResult TagOpenTrim = new(true, true);
        public static readonly TagResult TagClose = new(false, false);
        public static readonly TagResult TagCloseTrim = new(false, true);

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
        public static Parser<TagResult> TagStart() => new TagStartParser();
        public static Parser<TagResult> TagEnd() => new TagEndParser();
        public static Parser<TagResult> OutputTagStart() => new OutputTagStartParser();
        public static Parser<TagResult> OutputTagEnd() => new OutputTagEndParser();

        private sealed class TagStartParser : Parser<TagResult>
        {
            public override bool Parse(ParseContext context, ref ParseResult<TagResult> result)
            {
                context.EnterParser(this);

                var start = context.Scanner.Cursor.Position;

                var p = (FluidParseContext)context;

                if (p.LiquidTagDepth > 0)
                {
                    result.Set(start.Offset, context.Scanner.Cursor.Offset, TagResult.TagOpen);

                    context.ExitParser(this);
                    return true;
                }

#if NET6_0_OR_GREATER
                if (context.Scanner.ReadText("{%"))
#else
                if (context.Scanner.ReadChar('{') && context.Scanner.ReadChar('%'))
#endif
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
        /// Search for `%}`, `-%}` to close a tag.
        /// Also, if the tag is inside a `liquid` tag, it will only look for a new line to close the tag.
        /// </summary>
        private sealed class TagEndParser : Parser<TagResult>
        {
            public bool SkipWhitespace { get; set; } = true;

            public override bool Parse(ParseContext context, ref ParseResult<TagResult> result)
            {
                var p = (FluidParseContext)context;

                var newLineIsPresent = false;

                var start = context.Scanner.Cursor.Position;

                if (SkipWhitespace)
                {
                    if (p.LiquidTagDepth > 0)
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

                bool trim;

                if (p.LiquidTagDepth > 0)
                {
                    if (newLineIsPresent)
                    {
                        result.Set(start.Offset, context.Scanner.Cursor.Offset, TagResult.TagClose);
                        return true;
                    }
                    else
                    {
                        trim = context.Scanner.ReadChar('-');

#if NET6_0_OR_GREATER
                        if (context.Scanner.ReadText("%}"))
#else
                        if (context.Scanner.ReadChar('%') && context.Scanner.ReadChar('}'))
#endif
                        {
                            p.StripNextTextSpanStatement = trim;
                            p.PreviousTextSpanStatement = null;
                            p.PreviousIsTag = true;
                            p.PreviousIsOutput = false;

                            context.Scanner.Cursor.ResetPosition(start);

                            result.Set(start.Offset, context.Scanner.Cursor.Offset, trim ? TagResult.TagCloseTrim : TagResult.TagClose);
                            return true;
                        }

                        context.Scanner.Cursor.ResetPosition(start);
                        return false;
                    }
                }

                trim = context.Scanner.ReadChar('-');

#if NET6_0_OR_GREATER
                if (context.Scanner.ReadText("%}"))
#else
                if (context.Scanner.ReadChar('%') && context.Scanner.ReadChar('}'))
#endif
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

        private sealed class OutputTagStartParser : Parser<TagResult>
        {
            public override bool Parse(ParseContext context, ref ParseResult<TagResult> result)
            {
                var start = context.Scanner.Cursor.Position;

#if NET6_0_OR_GREATER
                if (context.Scanner.ReadText("{{"))
#else
                if (context.Scanner.ReadChar('{') && context.Scanner.ReadChar('{'))
#endif
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

        private sealed class OutputTagEndParser : Parser<TagResult>
        {
            public override bool Parse(ParseContext context, ref ParseResult<TagResult> result)
            {
                context.EnterParser(this);

                var start = context.Scanner.Cursor.Position;

                context.SkipWhiteSpace();

                var trim = context.Scanner.ReadChar('-');

#if NET6_0_OR_GREATER
                if (context.Scanner.ReadText("}}"))
#else
                if (context.Scanner.ReadChar('}') && context.Scanner.ReadChar('}'))
#endif
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

    public static class NonInlineLiquidTagParsers
    {
        public static Parser<TagResult> TagStart() => new TagStartParser();
        public static Parser<TagResult> TagEnd() => new TagEndParser();
        public static Parser<TagResult> OutputTagStart() => new OutputTagStartParser();
        public static Parser<TagResult> OutputTagEnd() => new OutputTagEndParser();

        private sealed class TagStartParser : Parser<TagResult>, ISeekable
        {
            public bool CanSeek => true;
            public char[] ExpectedChars { get; set; } = ['{'];
            public bool SkipWhitespace { get; } = false;

            public override bool Parse(ParseContext context, ref ParseResult<TagResult> result)
            {
                context.EnterParser(this);

                var start = context.Scanner.Cursor.Position;

                var p = (FluidParseContext)context;

#if NET6_0_OR_GREATER
                if (context.Scanner.ReadText("{%"))
#else
                if (context.Scanner.ReadChar('{') && context.Scanner.ReadChar('%'))
#endif
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

        private sealed class TagEndParser : Parser<TagResult>, ISeekable
        {
            public bool CanSeek => true;
            public char[] ExpectedChars { get; set; } = ['-', '%'];
            public bool SkipWhitespace { get; set; } = false;

            public override bool Parse(ParseContext context, ref ParseResult<TagResult> result)
            {
                var p = (FluidParseContext)context;

                var start = context.Scanner.Cursor.Position;

                bool trim;

                trim = context.Scanner.ReadChar('-');

#if NET6_0_OR_GREATER
                if (context.Scanner.ReadText("%}"))
#else
                if (context.Scanner.ReadChar('%') && context.Scanner.ReadChar('}'))
#endif
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
            public bool CanSeek => true;

            public char[] ExpectedChars { get; set; } = ['{'];

            public bool SkipWhitespace { get; } = false;

            public override bool Parse(ParseContext context, ref ParseResult<TagResult> result)
            {
                var start = context.Scanner.Cursor.Position;

#if NET6_0_OR_GREATER
                if (context.Scanner.ReadText("{{"))
#else
                if (context.Scanner.ReadChar('{') && context.Scanner.ReadChar('{'))
#endif
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
            public bool CanSeek => true;

            public char[] ExpectedChars { get; set; } = ['-', '}'];

            public bool SkipWhitespace { get; } = false;

            public override bool Parse(ParseContext context, ref ParseResult<TagResult> result)
            {
                context.EnterParser(this);

                var start = context.Scanner.Cursor.Position;

                var trim = context.Scanner.ReadChar('-');

#if NET6_0_OR_GREATER
                if (context.Scanner.ReadText("}}"))
#else
                if (context.Scanner.ReadChar('}') && context.Scanner.ReadChar('}'))
#endif
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
