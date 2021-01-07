using Fluid.Ast;
using Fluid.Ast.BinaryExpressions;
using Fluid.Values;
using Parlot;
using Parlot.Fluent;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using static Parlot.Fluent.Parsers;

namespace Fluid.Parlot
{
    public class ParlotParser : IFluidParser
    {
        public IParser<List<Statement>> Grammar;
        public Dictionary<string, IParser<Statement>> CustomTags { get; } = new();

        protected static readonly IParser<char> LBrace = Terms.Char('{');
        protected static readonly IParser<char> RBrace = Terms.Char('}');
        protected static readonly IParser<char> LParen = Terms.Char('(');
        protected static readonly IParser<char> RParen = Terms.Char(')');
        protected static readonly IParser<char> LBracket = Literals.Char('[');
        protected static readonly IParser<char> RBracket = Terms.Char(']');
        protected static readonly IParser<char> Equal = Terms.Char('=');
        protected static readonly IParser<char> Colon = Terms.Char(':');
        protected static readonly IParser<char> Comma = Terms.Char(',');
        protected static readonly IParser<char> Dot = Literals.Char('.');
        protected static readonly IParser<char> Pipe = Terms.Char('|');

        protected static readonly IParser<TextSpan> String = Terms.String(StringLiteralQuotes.SingleOrDouble);
        protected static readonly IParser<decimal> Number = Terms.Decimal(NumberOptions.AllowSign);
        protected static readonly IParser<string> True = Terms.Text("true");
        protected static readonly IParser<string> False = Terms.Text("false");

        protected static readonly IParser<string> DoubleEquals = Terms.Text("==");
        protected static readonly IParser<string> NotEquals = Terms.Text("!=");
        protected static readonly IParser<string> Different = Terms.Text("<>");
        protected static readonly IParser<string> Greater = Terms.Text(">");
        protected static readonly IParser<string> Lower = Terms.Text("<");
        protected static readonly IParser<string> GreaterOr = Terms.Text(">=");
        protected static readonly IParser<string> LowerOr = Terms.Text("<=");
        protected static readonly IParser<string> Contains = Terms.Text("contains");
        protected static readonly IParser<string> BinaryOr = Terms.Text("or");
        protected static readonly IParser<string> BinaryAnd = Terms.Text("and");

        protected static readonly IParser<TextSpan> Identifier = Terms.Identifier(extraPart: static c => c == '-');

        protected static readonly IDeferredParser<Expression> Primary = Deferred<Expression>();
        protected static readonly IDeferredParser<Expression> LogicalExpression = Deferred<Expression>();
        protected static readonly IDeferredParser<Expression> FilterExpression = Deferred<Expression>();
        protected readonly IDeferredParser<List<Statement>> TagsList = Deferred<List<Statement>>();

        protected static readonly IParser<TagResult> OutputStart = TagParsers.OutputTagStart();
        protected static readonly IParser<TagResult> OutputEnd = TagParsers.OutputTagEnd(true);
        protected static readonly IParser<TagResult> TagStart = TagParsers.TagStart();
        protected static readonly IParser<TagResult> TagStartSpaced = TagParsers.TagStart(true);
        protected static readonly IParser<TagResult> TagEnd = TagParsers.TagEnd(true);


        // These tags are not parsed when expecting any tag, but should not be marked as invalid so we can detect {% without correct values
        protected readonly HashSet<string> ExpectedTags = new()
        {
            "endcomment", "endcapture", "endraw", "else", "elsif", "endif", "endunless", "when", "endfor", "endcase"
        };

        // The return type of the generated method is the generic type of the parser

        Expression ParseInteger(NumberOptions numberOptions)
        {
            _context.Scanner.SkipWhiteSpace();

            var start = _context.Scanner.Cursor.Offset;

            if ((numberOptions & NumberOptions.AllowSign) == NumberOptions.AllowSign)
            {
                if (!_context.Scanner.ReadChar('-'))
                {
                    // If there is no '-' try to read a '+' but don't read both.
                    _context.Scanner.ReadChar('+');
                }
            }

            if (_context.Scanner.ReadInteger())
            {
                var end = _context.Scanner.Cursor.Offset;

                if (long.TryParse(_context.Scanner.Buffer.AsSpan(start, end - start), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
                {
                    return new LiteralExpression(FluidValue.Create(value));
                }
            }

            return null;
        }

        public Expression ParseIndexer()
        {
            if (!_context.Scanner.ReadChar('['))
            {
                return null;
            }

            var primary = ParsePrimary();

            if (primary == null)
            {
                return null;
            }

            if (!_context.Scanner.ReadChar(']'))
            {
                return null;
            }

            return primary;
        }

        public Expression ParseString(StringLiteralQuotes quotes)
        {
            var start = _context.Scanner.Cursor.Offset;

            var success = quotes switch
            {
                StringLiteralQuotes.Single => _context.Scanner.ReadSingleQuotedString(),
                StringLiteralQuotes.Double => _context.Scanner.ReadDoubleQuotedString(),
                StringLiteralQuotes.SingleOrDouble => _context.Scanner.ReadQuotedString(),
                _ => false
            };

            var end = _context.Scanner.Cursor.Offset;

            if (success)
            {
                // Remove quotes
                var encoded = _context.Scanner.Buffer.AsSpan(start + 1, end - start - 2);
                var decoded = Character.DecodeString(encoded);

                // Don't create a new string if the decoded string is the same, meaning is 
                // has no escape sequences.
                var span = decoded == encoded || decoded.SequenceEqual(encoded)
                    ? new TextSpan(_context.Scanner.Buffer, start + 1, encoded.Length)
                    : new TextSpan(decoded.ToString());

                return new LiteralExpression(FluidValue.Create(span.ToString()));
            }
            else
            {
                return null;
            }
        }

        public Expression ParsePrimary()
        {
            var number = ParseNumber(NumberOptions.AllowSign);

            if (number != null)
            {
                return number;
            }

            var stringValue = ParseString(StringLiteralQuotes.SingleOrDouble);

            if (stringValue != null)
            {
                return stringValue;
            }    

            return null;
        }

        public Expression ParseNumber(NumberOptions numberOptions)
        {
            var start = _context.Scanner.Cursor.Offset;

            if ((numberOptions & NumberOptions.AllowSign) == NumberOptions.AllowSign)
            {
                if (!_context.Scanner.ReadChar('-'))
                {
                    // If there is no '-' try to read a '+' but don't read both.
                    _context.Scanner.ReadChar('+');
                }
            }

            if (_context.Scanner.ReadDecimal())
            {
                var end = _context.Scanner.Cursor.Offset;

                if (decimal.TryParse(_context.Scanner.Buffer.AsSpan(start, end - start), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
                {
                    return new LiteralExpression(FluidValue.Create(value));
                }
            }

            if (_context.Scanner.ReadText("true"))
            {
                return new LiteralExpression(BooleanValue.True);
            }

            if (_context.Scanner.ReadText("false"))
            {
                return new LiteralExpression(BooleanValue.False);
            }

            return null;
        }

        public ParlotParser()
        {
            var Integer = Terms.Integer().Then<Expression>(x => new LiteralExpression(FluidValue.Create((decimal)x)));

            // Member expressions
            var Indexer = Between(LBracket, Primary, RBracket).Then<MemberSegment>(x => new IndexerSegment(x));

            var Member = Identifier.Then<MemberSegment>(x => new IdentifierSegment(x.ToString())).And(
                ZeroOrMany(
                    Dot.SkipAnd(Identifier.Then<MemberSegment>(x => new IdentifierSegment(x.ToString())))
                    .Or(Indexer)))
                .Then(x =>
                {
                    x.Item2.Insert(0, x.Item1);
                    return new MemberExpression(x.Item2);
                });
                
            var Range = LParen
                .SkipAnd(OneOf(Integer, Member.Then<Expression>(x => x)))
                .AndSkip(Terms.Text(".."))
                .And(OneOf(Integer, Member.Then<Expression>(x => x)))
                .AndSkip(RParen)
                .Then(x => new RangeExpression(x.Item1, x.Item2));

            // primary => NUMBER | STRING | BOOLEAN | property
            Primary.Parser =
                Number.Then<Expression>(x => new LiteralExpression(FluidValue.Create(x)))
                .Or(String.Then<Expression>(x => new LiteralExpression(FluidValue.Create(x.ToString()))))
                .Or(True.Then<Expression>(x => new LiteralExpression(BooleanValue.True)))
                .Or(False.Then<Expression>(x => new LiteralExpression(BooleanValue.False)))
                .Or(Member.Then<Expression>(x => x))
                ;

            var CaseValueList = Separated(BinaryOr, Primary);
            
            // TODO: 'and' has a higher priority than 'or', either create a new scope, or implement operators priority

            var Logical = Primary.And(Star(OneOf(BinaryOr, BinaryAnd, Contains).And(Primary)))
                .Then(static x =>
                {
                    var result = x.Item1;

                    foreach (var op in x.Item2)
                    {
                        result = op.Item1 switch
                        {
                            "or" => new OrBinaryExpression(result, op.Item2),
                            "and" => new AndBinaryExpression(result, op.Item2),
                            "contains" => new ContainsBinaryExpression(result, op.Item2),
                            _ => null
                        };
                    }

                    return result;
                });

            var Comparison = Logical.And(Star(OneOf(DoubleEquals, NotEquals, Different, GreaterOr, LowerOr, Greater, Lower).And(Logical)))
                .Then(static x =>
                {
                    var result = x.Item1;

                    foreach (var op in x.Item2)
                    {
                        result = op.Item1 switch
                        {
                            "==" => new EqualBinaryExpression(result, op.Item2),
                            "!=" => new NotEqualBinaryExpression(result, op.Item2),
                            "<>" => new NotEqualBinaryExpression(result, op.Item2),
                            ">" => new GreaterThanBinaryExpression(result, op.Item2, true),
                            "<" => new LowerThanExpression(result, op.Item2, true),
                            ">=" => new GreaterThanBinaryExpression(result, op.Item2, false),
                            "<=" => new LowerThanExpression(result, op.Item2, false),
                            _ => null
                        };
                    }

                    return result;
                });

            // Primary ( | identifer ( ':' (name : value)+ )! ] )*
            FilterExpression.Parser = Primary
                .And(ZeroOrMany(Pipe.SkipAnd(Identifier)
                    .And(ZeroOrOne(Colon.SkipAnd(
                        Separated(Comma,
                            OneOf(Primary.Then(static x => new FilterArgument(null, x)),
                            Identifier.AndSkip(Colon).And(Primary).Then(static x => new FilterArgument(x.Item1.ToString(), x.Item2))
                            ))
                        )))
                    ))
                .Then(x =>
                    {
                        // Primary
                        var result = x.Item1;

                        // Filters
                        foreach(var pipeResult in x.Item2)
                        {
                            var identifier = pipeResult.Item1.ToString();
                            var arguments = pipeResult.Item2;

                            result = new FilterExpression(result, identifier, arguments ?? new List<FilterArgument>());
                        }

                        return result;
                    });

            LogicalExpression.Parser = Comparison;

            var Output = OutputStart.SkipAnd(FilterExpression.And(OutputEnd)
                .Then<Statement>(static x => new OutputStatement(x.Item1))
                .ElseError("Invalid tag, expected an expression")
                );

            var OtherTags = Identifier.ElseError($"Invalid tag").Switch((context, previous) =>
            {
                var tagName = previous.ToString();

                if (CustomTags.TryGetValue(tagName, out var parser))
                {
                    return parser;
                }

                if (!ExpectedTags.Contains(tagName))
                {
                    // A {% TAGNAME was found without possible handler, the template is invalid
                    throw new ParseException($"Invalid tag '{tagName}'");
                }

                return null;
            });

            var BreakTag = TagEnd.Then<Statement>(x => new BreakStatement());
            var ContinueTag = TagEnd.Then<Statement>(x => new ContinueStatement());
            var CommentTag = TagEnd
                        .SkipAnd(AnyCharBefore(CreateTag("endcomment")))
                        .AndSkip(CreateTag("endcomment"))
                        .Then<Statement>(x => new CommentStatement(x))
                        .ElseError("Invalid 'comment' tag")
                        ;
            var CaptureTag = Identifier
                        .AndSkip(TagEnd)
                        .And(TagsList)
                        .AndSkip(CreateTag("endcapture"))
                        .Then<Statement>(x => new CaptureStatement(x.Item1.ToString(), x.Item2))
                        .ElseError("Invalid 'capture' tag")
                        ;
            var CycleTag = ZeroOrOne(Identifier.AndSkip(Colon).Then(x => x))
                        .And(Separated(Comma, String))
                        .AndSkip(TagEnd)
                        .Then<Statement>(x =>
                        {
                            var group = x.Item1.Length == 1
                                ? new LiteralExpression(FluidValue.Create(x.Item1))
                                : null;

                            var values = x.Item2.Select(e => new LiteralExpression(FluidValue.Create(e.ToString()))).ToList<Expression>();

                            return new CycleStatement(group, values);
                        })
                        .ElseError("Invalid 'cycle' tag")
                        ;
            var DecrementTag = Identifier.AndSkip(TagEnd).Then<Statement>(x => new DecrementStatement(x.ToString()));
            var IncludeTag = OneOf(
                        Primary.AndSkip(Comma).And(Separated(Comma, Identifier.AndSkip(Colon).And(Primary).Then(static x => new AssignStatement(x.Item1.ToString(), x.Item2)))).Then(x => new IncludeStatement(this, x.Item1, null, x.Item2)),
                        Primary.AndSkip(Terms.Text("with")).And(Primary).Then(x => new IncludeStatement(this, x.Item1, with: x.Item2)),
                        Primary.Then(x => new IncludeStatement(this, x))
                        ).AndSkip(TagEnd)
                        .Then<Statement>(x => x)
                        .ElseError("Invalid 'include' tag");
            var IncrementTag = Identifier.AndSkip(TagEnd).Then<Statement>(x => new IncrementStatement(x.ToString()));
            var RawTag = TagEnd.SkipAnd(AnyCharBefore(CreateTag("endraw"), consumeDelimiter: true, failOnEof: true).Then<Statement>(x => new RawStatement(x))).ElseError("Not end tag found for {% raw %}");
            var AssignTag = Identifier.AndSkip(Equal).And(FilterExpression).AndSkip(TagEnd).Then<Statement>(x => new AssignStatement(x.Item1.ToString(), x.Item2));
            var IfTag = LogicalExpression
                        .AndSkip(TagEnd)
                        .And(TagsList)
                        .And(ZeroOrMany(
                            TagStart.SkipAnd(Terms.Text("elsif")).SkipAnd(LogicalExpression).AndSkip(TagEnd).And(TagsList))
                            .Then(x => x.Select(e => new ElseIfStatement(e.Item1, e.Item2)).ToList()))
                        .And(ZeroOrOne(
                            CreateTag("else").SkipAnd(TagsList))
                            .Then(x => x != null ? new ElseStatement(x) : null))
                        .AndSkip(CreateTag("endif"))
                        .Then<Statement>(x => new IfStatement(x.Item1, x.Item2, x.Item4, x.Item3))
                        .ElseError("Invalid 'if' tag");
            var UnlessTag = LogicalExpression
                        .AndSkip(TagEnd)
                        .And(TagsList)
                        .AndSkip(CreateTag("endunless"))
                        .Then<Statement>(x => new UnlessStatement(x.Item1, x.Item2))
                        .ElseError("Invalid 'unless' tag");
            var CaseTag = Primary
                       .AndSkip(TagEnd)
                       .AndSkip(AnyCharBefore(TagStart, canBeEmpty: true))
                       .And(ZeroOrMany(
                           TagStart.Then(x => x).AndSkip(Terms.Text("when")).And(CaseValueList.ElseError("Invalid 'when' tag")).AndSkip(TagEnd).And(TagsList))
                           .Then(x => x.Select(e => new WhenStatement(e.Item2, e.Item3)).ToList()))
                       .And(ZeroOrOne(
                           CreateTag("else").SkipAnd(TagsList))
                           .Then(x => x != null ? new ElseStatement(x) : null))
                       .AndSkip(CreateTag("endcase"))
                       .Then<Statement>(x => new CaseStatement(x.Item1, x.Item3, x.Item2))
                       .ElseError("Invalid 'case' tag");
            var ForTag = OneOf(
                            Identifier
                            .AndSkip(Terms.Text("in"))
                            .And(Member)
                            .And(ZeroOrMany(OneOf( // Use * since each can appear in any order. Validation is done once it's parsed
                                Terms.Text("reversed").Then(x => new ForModifier { IsReversed = true }),
                                Terms.Text("limit").SkipAnd(Colon).SkipAnd(Integer).Then(x => new ForModifier { IsLimit = true, Value = x }),
                                Terms.Text("offset").SkipAnd(Colon).SkipAnd(Integer).Then(x => new ForModifier { IsOffset = true, Value = x })
                                )))
                            .AndSkip(TagEnd)
                            .And(TagsList)
                            .And(ZeroOrOne(
                                CreateTag("else").SkipAnd(TagsList))
                                .Then(x => x != null ? new ElseStatement(x) : null))
                            .AndSkip(CreateTag("endfor"))
                            .Then<Statement>(x =>
                            {
                                var identifier = x.Item1.ToString();
                                var member = x.Item2;
                                var statements = x.Item4;
                                var elseStatement = x.Item5;

                                var limitResult = x.Item3.Where(l => l.IsLimit).LastOrDefault().Value;
                                var offsetResult = x.Item3.Where(l => l.IsOffset).LastOrDefault().Value;
                                var reversed = x.Item3.Any(l => l.IsReversed);

                                return new ForStatement(statements, identifier, member, limitResult, offsetResult, reversed, elseStatement);
                            }),

                            Identifier
                            .AndSkip(Terms.Text("in"))
                            .And(Range)
                            .And(ZeroOrMany(OneOf( // Use * since each can appear in any order. Validation is done once it's parsed
                                Terms.Text("reversed").Then(x => new ForModifier { IsReversed = true }),
                                Terms.Text("limit").SkipAnd(Colon).SkipAnd(Integer).Then(x => new ForModifier { IsLimit = true, Value = x }),
                                Terms.Text("offset").SkipAnd(Colon).SkipAnd(Integer).Then(x => new ForModifier { IsOffset = true, Value = x })
                                )))
                            .AndSkip(TagEnd)
                            .And(TagsList)
                            .AndSkip(CreateTag("endfor"))
                            .Then<Statement>(x =>
                            {
                                var identifier = x.Item1.ToString();
                                var range = x.Item2;
                                var statements = x.Item4;

                                var limitResult = x.Item3.Where(l => l.IsLimit).LastOrDefault().Value;
                                var offsetResult = x.Item3.Where(l => l.IsOffset).LastOrDefault().Value;
                                var reversed = x.Item3.Any(l => l.IsReversed);

                                return new ForStatement(statements, identifier, range, limitResult, offsetResult, reversed, null);

                            })
                        ).ElseError("Invalid 'for' tag");

            var KnownTags = Identifier.Switch((context, previous) =>
            {
                // perf: this lambda is allocated only once since the KnownTags parser is reused

                // Because tags like 'else' are not listed, they won't count in TagsList, and will stop being processed 
                // as inner tags in blocks like {% if %} TagsList {% endif $}

                switch (previous.ToString())
                {
                    case "break": return BreakTag;
                    case "continue": return ContinueTag;
                    case "comment": return CommentTag;
                    case "capture": return CaptureTag;
                    case "cycle": return CycleTag;
                    case "decrement": return DecrementTag;
                    case "include": return IncludeTag;
                    case "increment": return IncrementTag;
                    case "raw": return RawTag;
                    case "assign": return AssignTag;
                    case "if": return IfTag;
                    case "unless": return UnlessTag;
                    case "case": return CaseTag;
                    case "for": return ForTag;

                    default: return null;
                };
            });

            var Tag = TagStart.SkipAnd(KnownTags.Or(OtherTags));

            var Text = AnyCharBefore(OutputStart.Or(TagStart))
                .Then(static x => new TextSpanStatement(x));

            TagsList.Parser = Star(
                Output
                .Or(Tag)
                .Or(Text.Then<Statement>((ctx, x) => { var p = (ParlotContext)ctx; p.PreviousTextSpanStatement = x; if (p.StripNextTextSpanStatement) { x.StrippedLeft = true; p.StripNextTextSpanStatement = false;  } return x; })));

            Grammar = TagsList;
        }

        public IParser<string> CreateTag(string tagName) => TagStart.SkipAnd(Terms.Text(tagName)).AndSkip(TagEnd);

        public void RegisterEmptyTag(string tagName, Func<EmptyTagStatement, TextWriter, TextEncoder, TemplateContext, ValueTask<Completion>> render)
        {
            CustomTags[tagName] = TagEnd.Then<Statement>(x => new EmptyTagStatement(render));
        }

        public void RegisterIdentifierTag(string tagName, Func<IdentifierTagStatement, TextWriter, TextEncoder, TemplateContext, ValueTask<Completion>> render)
        {
            CustomTags[tagName] = Identifier.AndSkip(TagEnd).Then<Statement>(x => new IdentifierTagStatement(x.ToString(), render));
        }

        public void RegisterEmptyBlock(string tagName, Func<EmptyBlockStatement, TextWriter, TextEncoder, TemplateContext, ValueTask<Completion>> render)
        {
            ExpectedTags.Add("end" + tagName);
            CustomTags[tagName] = TagEnd.SkipAnd(TagsList).AndSkip(CreateTag("end" + tagName)).Then<Statement>(x => new EmptyBlockStatement(x, render));
        }

        public void RegisterIdentifierBlock(string tagName, Func<ParserBlockStatement<TextSpan>, TextWriter, TextEncoder, TemplateContext, ValueTask<Completion>> render)
        {
            RegisterParserBlock(tagName, Identifier, render);
        }

        public void RegisterPrimaryExpressionBlock(string tagName, Func<ParserBlockStatement<Expression>, TextWriter, TextEncoder, TemplateContext, ValueTask<Completion>> render)
        {
            RegisterParserBlock(tagName, Primary, render);
        }

        public void RegisterParserBlock<T>(string tagName, IParser<T> parser, Func<ParserBlockStatement<T>, TextWriter, TextEncoder, TemplateContext, ValueTask<Completion>> render)
        {
            ExpectedTags.Add("end" + tagName);
            CustomTags[tagName] = parser.AndSkip(TagEnd).And(TagsList).AndSkip(CreateTag("end" + tagName)).Then<Statement>(x => new ParserBlockStatement<T>(x.Item1, x.Item2, render));
        }

        private ParlotContext _context;

        public IFluidTemplate Parse(string template)
        {
            var context = new ParlotContext(template);

            var success = Grammar.TryParse(context, out var statements, out var parlotError);

            if (parlotError != null)
            {
                throw new ParseException($"{parlotError.Message} at {parlotError.Position}");
            }

            if (!success)
            {
                return null;
            }

            return new ParlotTemplate(statements);
        }

        public IFluidTemplate Parse2(string template)
        {
            _context = new ParlotContext(template);

            //var success = Grammar.TryParse(context, out var statements, out var parlotError);

            //if (parlotError != null)
            //{
            //    throw new ParseException($"{parlotError.Message} at {parlotError.Position}");
            //}

            //if (!success)
            //{
            //    return null;
            //}

            return new ParlotTemplate(null);
        }
    }
}
