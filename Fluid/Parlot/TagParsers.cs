using Parlot;
using Parlot.Fluent;

namespace Fluid.Parlot
{
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
        public static IParser<TagResult> TagStart(ParlotParser parser, bool skipWhiteSpace = false) => new TagStartParser(parser, skipWhiteSpace);
        public static IParser<TagResult> TagEnd(ParlotParser parser, bool skipWhiteSpace = false) => new TagEndParser(parser, skipWhiteSpace);
        public static IParser<TagResult> OutputTagStart(ParlotParser parser, bool skipWhiteSpace = false) => new OutputTagStartParser(parser, skipWhiteSpace);
        public static IParser<TagResult> OutputTagEnd(ParlotParser parser, bool skipWhiteSpace = false) => new OutputTagEndParser(parser, skipWhiteSpace);

        private class TagStartParser : global::Parlot.Fluent.Parser<TagResult>
        {
            private readonly ParlotParser _parser;
            private readonly bool _skipWhiteSpace;

            public TagStartParser(ParlotParser parser, bool skipWhiteSpace = false)
            {
                _parser = parser;
                _skipWhiteSpace = skipWhiteSpace;
            }

            public override bool Parse(ParseContext context, ref ParseResult<TagResult> result)
            {
                if (_skipWhiteSpace)
                {
                    context.Scanner.SkipWhiteSpace();
                }

                var start = context.Scanner.Cursor.Position;

                if (context.Scanner.ReadChar('{') && context.Scanner.ReadChar('%'))
                {
                    var p = (ParlotContext)context;

                    var trim = context.Scanner.ReadChar('-');

                    if (trim)
                    {
                        p.PreviousTextSpanStatement?.StripRight();
                    }

                    p.PreviousTextSpanStatement = null;

                    result.Set(context.Scanner.Buffer, start, context.Scanner.Cursor.Position, Name, new TagResult(true, trim));
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
            private readonly ParlotParser _parser;
            private readonly bool _skipWhiteSpace;

            public TagEndParser(ParlotParser parser, bool skipWhiteSpace = false)
            {
                _parser = parser;
                _skipWhiteSpace = skipWhiteSpace;
            }

            public override bool Parse(ParseContext context, ref ParseResult<TagResult> result)
            {
                if (_skipWhiteSpace)
                {
                    context.Scanner.SkipWhiteSpace();
                }

                var start = context.Scanner.Cursor.Position;

                bool trim = context.Scanner.ReadChar('-');

                if (context.Scanner.ReadChar('%') && context.Scanner.ReadChar('}'))
                {
                    var p = (ParlotContext)context;

                    p.StripNextTextSpanStatement = trim;
                    p.PreviousTextSpanStatement = null;

                    result.Set(context.Scanner.Buffer, start, context.Scanner.Cursor.Position, Name, new TagResult(false, trim));
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
            private readonly ParlotParser _parser;
            private readonly bool _skipWhiteSpace;

            public OutputTagStartParser(ParlotParser parser, bool skipWhiteSpace = false)
            {
                _parser = parser;
                _skipWhiteSpace = skipWhiteSpace;
            }

            public override bool Parse(ParseContext context, ref ParseResult<TagResult> result)
            {
                if (_skipWhiteSpace)
                {
                    context.Scanner.SkipWhiteSpace();
                }

                var start = context.Scanner.Cursor.Position;

                if (context.Scanner.ReadChar('{') && context.Scanner.ReadChar('{'))
                {
                    var trim = context.Scanner.ReadChar('-');

                    var p = (ParlotContext)context;

                    if (trim)
                    {
                        p.PreviousTextSpanStatement?.StripRight();
                    }

                    p.PreviousTextSpanStatement = null;


                    result.Set(context.Scanner.Buffer, start, context.Scanner.Cursor.Position, Name, new TagResult(true, trim));
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
            private readonly ParlotParser _parser;
            private readonly bool _skipWhiteSpace;

            public OutputTagEndParser(ParlotParser parser, bool skipWhiteSpace = false)
            {
                _parser = parser;
                _skipWhiteSpace = skipWhiteSpace;
            }

            public override bool Parse(ParseContext context, ref ParseResult<TagResult> result)
            {
                if (_skipWhiteSpace)
                {
                    context.Scanner.SkipWhiteSpace();
                }

                var start = context.Scanner.Cursor.Position;

                bool trim = context.Scanner.ReadChar('-');

                if (context.Scanner.ReadChar('}') && context.Scanner.ReadChar('}'))
                {
                    var p = (ParlotContext)context;

                    p.StripNextTextSpanStatement = trim;
                    p.PreviousTextSpanStatement = null;

                    result.Set(context.Scanner.Buffer, start, context.Scanner.Cursor.Position, Name, new TagResult(false, trim));
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
