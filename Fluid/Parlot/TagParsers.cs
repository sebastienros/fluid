using Fluid.Ast;
using Parlot;
using Parlot.Fluent;

namespace Fluid.Parlot
{
    public struct ForModifier
    {
        public bool IsReversed;
        public bool IsLimit;
        public bool IsOffset;
        public Expression Value;
    }

    public struct TagResult
    {
        public TagResult(bool open, bool trim)
        {
            Open = open;
            Trim = trim;
        }

        public bool Open;
        public bool Trim;
    }

    public static class TagParsers
    {
        public static Parser<TagResult> TagStart(bool skipWhiteSpace = false) => new TagStartParser(skipWhiteSpace);
        public static Parser<TagResult> TagEnd(bool skipWhiteSpace = false) => new TagEndParser(skipWhiteSpace);
        public static Parser<TagResult> OutputTagStart(bool skipWhiteSpace = false) => new OutputTagStartParser(skipWhiteSpace);
        public static Parser<TagResult> OutputTagEnd(bool skipWhiteSpace = false) => new OutputTagEndParser(skipWhiteSpace);

        private class TagStartParser : global::Parlot.Fluent.Parser<TagResult>
        {
            private readonly bool _skipWhiteSpace;

            public TagStartParser(bool skipWhiteSpace = false)
            {
                _skipWhiteSpace = skipWhiteSpace;
            }

            public override bool Parse(ParseContext context, ref ParseResult<TagResult> result)
            {
                if (_skipWhiteSpace)
                {
                    context.Scanner.SkipWhiteSpace();
                }

                var start = context.Scanner.Cursor.Offset;

                if (context.Scanner.ReadChar('{') && context.Scanner.ReadChar('%'))
                {
                    var p = (ParlotContext)context;

                    var trim = context.Scanner.ReadChar('-');

                    if (trim)
                    {
                        p.PreviousTextSpanStatement?.StripRight();
                    }

                    p.PreviousTextSpanStatement = null;

                    result.Set(start, context.Scanner.Cursor.Offset, new TagResult(true, trim));
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        private class TagEndParser : global::Parlot.Fluent.Parser<TagResult>
        {
            private readonly bool _skipWhiteSpace;

            public TagEndParser(bool skipWhiteSpace = false)
            {
                _skipWhiteSpace = skipWhiteSpace;
            }

            public override bool Parse(ParseContext context, ref ParseResult<TagResult> result)
            {
                if (_skipWhiteSpace)
                {
                    context.Scanner.SkipWhiteSpace();
                }

                var start = context.Scanner.Cursor.Offset;

                bool trim = context.Scanner.ReadChar('-');

                if (context.Scanner.ReadChar('%') && context.Scanner.ReadChar('}'))
                {
                    var p = (ParlotContext)context;

                    p.StripNextTextSpanStatement = trim;
                    p.PreviousTextSpanStatement = null;

                    result.Set(start, context.Scanner.Cursor.Offset, new TagResult(false, trim));
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        private class OutputTagStartParser : global::Parlot.Fluent.Parser<TagResult>
        {
            private readonly bool _skipWhiteSpace;

            public OutputTagStartParser(bool skipWhiteSpace = false)
            {
                _skipWhiteSpace = skipWhiteSpace;
            }

            public override bool Parse(ParseContext context, ref ParseResult<TagResult> result)
            {
                if (_skipWhiteSpace)
                {
                    context.Scanner.SkipWhiteSpace();
                }

                var start = context.Scanner.Cursor.Offset;

                if (context.Scanner.ReadChar('{') && context.Scanner.ReadChar('{'))
                {
                    var trim = context.Scanner.ReadChar('-');

                    var p = (ParlotContext)context;

                    if (trim)
                    {
                        p.PreviousTextSpanStatement?.StripRight();
                    }

                    p.PreviousTextSpanStatement = null;


                    result.Set(start, context.Scanner.Cursor.Offset, new TagResult(true, trim));
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        private class OutputTagEndParser : global::Parlot.Fluent.Parser<TagResult>
        {
            private readonly bool _skipWhiteSpace;

            public OutputTagEndParser(bool skipWhiteSpace = false)
            {
                _skipWhiteSpace = skipWhiteSpace;
            }

            public override bool Parse(ParseContext context, ref ParseResult<TagResult> result)
            {
                if (_skipWhiteSpace)
                {
                    context.Scanner.SkipWhiteSpace();
                }

                var start = context.Scanner.Cursor.Offset;

                bool trim = context.Scanner.ReadChar('-');

                if (context.Scanner.ReadChar('}') && context.Scanner.ReadChar('}'))
                {
                    var p = (ParlotContext)context;

                    p.StripNextTextSpanStatement = trim;
                    p.PreviousTextSpanStatement = null;

                    result.Set(start, context.Scanner.Cursor.Offset, new TagResult(false, trim));
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }
    }
}
