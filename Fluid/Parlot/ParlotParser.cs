using Fluid.Ast;
using Fluid.Ast.BinaryExpressions;
using Fluid.Values;
using Parlot.Fluent;
using System;
using System.Collections.Generic;
using System.Linq;
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
        protected static readonly IParser<string> OutputStart = Literals.Text("{{");
        protected static readonly IParser<string> OutputEnd = Terms.Text("}}");
        protected static readonly IParser<string> TagStart = Literals.Text("{%");
        protected static readonly IParser<string> TagStartSpaced = Terms.Text("{%");
        protected static readonly IParser<string> TagEnd = Terms.Text("%}");
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

        protected static readonly IDeferredParser<Expression> Primary = Deferred<Expression>();
        protected static readonly IDeferredParser<Expression> LogicalExpression = Deferred<Expression>();
        protected static readonly IDeferredParser<Expression> FilterExpression = Deferred<Expression>();

        // These tags are not parsed when expecting any tag, but should not be marked as invalid so we can detect {% without correct values
        protected readonly static HashSet<string> ExpectedTags = new()
        {
            "endcomment", "endcapture", "endraw", "else", "elsif", "endif", "endunless", "when", "endfor", "endcase"
        };
        
        public ParlotParser()
        {
            // TODO: Remove TextStatement
            // TODO: read the next - and assign the skip whitespace property to the adjacent Text statement

            var Identifier = Terms.Identifier(extraPart: static c => c == '-');

            var Integer = Terms.Integer().Then<Expression>(x => new LiteralExpression(FluidValue.Create((decimal)x)));

            // Member expressions
            var Indexer = LBracket.And(Primary).And(RBracket).Then<MemberSegment>(x => new IndexerSegment(x.Item2));

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
                .And(OneOf(Integer, Member.Then<Expression>(x => x)))
                .And(Terms.Text(".."))
                .And(OneOf(Integer, Member.Then<Expression>(x => x)))
                .And(RParen)
                .Then(x => new RangeExpression(x.Item2, x.Item4));

            // primary => NUMBER | STRING | BOOLEAN property
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
                            Identifier.And(Colon).And(Primary).Then(static x => new FilterArgument(x.Item1.ToString(), x.Item3))
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

            var TagsList = Deferred<List<Statement>>();

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
                        .And(CreateTag("endcomment"))
                        .Then<Statement>(x => new CommentStatement(x.Item1))
                        .ElseError("Invalid 'comment' tag")
                        ;
            var CaptureTag = Identifier
                        .And(TagEnd)
                        .And(TagsList)
                        .And(CreateTag("endcapture"))
                        .Then<Statement>(x => new CaptureStatement(x.Item1.ToString(), x.Item3))
                        .ElseError("Invalid 'capture' tag")
                        ;
            var CycleTag = ZeroOrOne(Identifier.And(Colon).Then(x => x.Item1))
                        .And(Separated(Comma, String))
                        .And(TagEnd)
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
            var DecrementTag = Identifier.And(TagEnd).Then<Statement>(x => new DecrementStatement(x.Item1.ToString()));
            var IncludeTag = OneOf(
                        Primary.And(Comma).And(Separated(Comma, Identifier.And(Colon).And(Primary).Then(static x => new AssignStatement(x.Item1.ToString(), x.Item3)))).Then(x => new IncludeStatement(this, x.Item1, null, x.Item3)),
                        Primary.And(Terms.Text("with")).And(Primary).Then(x => new IncludeStatement(this, x.Item1, with: x.Item3)),
                        Primary.Then(x => new IncludeStatement(this, x))
                        ).And(TagEnd)
                        .Then<Statement>(x => x.Item1)
                        .ElseError("Invalid 'include' tag");
            var IncrementTag = Identifier.And(TagEnd).Then<Statement>(x => new IncrementStatement(x.Item1.ToString()));
            var RawTag = TagEnd.SkipAnd(AnyCharBefore(CreateTag("endraw"), consumeDelimiter: true, failOnEof: true).Then<Statement>(x => new RawStatement(x))).ElseError("Not end tag found for {% raw %}");
            var AssignTag = Identifier.And(Equal).And(FilterExpression).And(TagEnd).Then<Statement>(x => new AssignStatement(x.Item1.ToString(), x.Item3));
            var IfTag = LogicalExpression
                        .AndSkip(TagEnd)
                        .And(TagsList)
                        .And(ZeroOrMany(
                            TagStart.And(Terms.Text("elsif")).And(LogicalExpression).And(TagEnd).And(TagsList))
                            .Then(x => x.Select(e => new ElseIfStatement(e.Item3, e.Item5)).ToList()))
                        .And(ZeroOrOne(
                            CreateTag("else").SkipAnd(TagsList))
                            .Then(x => x != null ? new ElseStatement(x) : null))
                        .AndSkip(CreateTag("endif"))
                        .Then<Statement>(x => new IfStatement(x.Item1, x.Item2, x.Item4, x.Item3))
                        .ElseError("Invalid 'if' tag");
            var UnlessTag = LogicalExpression
                        .And(TagEnd)
                        .And(TagsList)
                        .And(CreateTag("endunless"))
                        .Then<Statement>(x => new UnlessStatement(x.Item1, x.Item3))
                        .ElseError("Invalid 'unless' tag");
            var CaseTag = Primary
                       .And(TagEnd)
                       .And(AnyCharBefore(TagStart, canBeEmpty: true))
                       .And(ZeroOrMany(
                           TagStart.Then(x => x).And(Terms.Text("when")).And(CaseValueList.ElseError("Invalid 'when' tag")).And(TagEnd).And(TagsList))
                           .Then(x => x.Select(e => new WhenStatement(e.Item3, e.Item5)).ToList()))
                       .And(ZeroOrOne(
                           CreateTag("else").SkipAnd(TagsList))
                           .Then(x => x != null ? new ElseStatement(x) : null))
                       .And(CreateTag("endcase"))
                       .Then<Statement>(x => new CaseStatement(x.Item1, x.Item5, x.Item4))
                       .ElseError("Invalid 'case' tag");
            var ForTag = OneOf(
                            Identifier
                            .And(Terms.Text("in"))
                            .And(Member)
                            .And(ZeroOrMany(OneOf( // Use * since each can appear in any order. Validation is done once it's parsed
                                Terms.Text("reversed").Named("reversed"),
                                Terms.Text("limit").SkipAnd(Colon).SkipAnd(Integer).Named("limit"),
                                Terms.Text("offset").SkipAnd(Colon).SkipAnd(Integer).Named("offset")
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
                                var member = x.Item3;
                                var statements = x.Item5;
                                var elseStatement = x.Item6;

                                var limitResult = x.Item4.LastOrDefault(l => l.ParserName == "limit");
                                var offsetResult = x.Item4.LastOrDefault(l => l.ParserName == "offset");

                                var limit = limitResult.Value as Expression ?? null;
                                var offset = offsetResult.Value as Expression ?? null;
                                var reversed = x.Item4.Any(l => l.ParserName == "reversed");

                                return new ForStatement(statements, identifier, member, limit, offset, reversed, elseStatement);
                            }),

                            Identifier
                            .And(Terms.Text("in"))
                            .And(Range)
                            .And(ZeroOrMany(OneOf( // Use * since each can appear in any order. Validation is done once it's parsed
                                Terms.Text("reversed").Named("reversed"),
                                Terms.Text("limit").SkipAnd(Colon).SkipAnd(Integer).Named("limit"),
                                Terms.Text("offset").SkipAnd(Colon).SkipAnd(Integer).Named("offset")
                                )))
                            .And(TagEnd)
                            .And(TagsList)
                            .And(CreateTag("endfor"))
                            .Then<Statement>(x =>
                            {
                                var identifier = x.Item1.ToString();
                                var range = x.Item3;
                                var statements = x.Item6;

                                var limitResult = x.Item4.LastOrDefault(l => l.ParserName == "limit");
                                var offsetResult = x.Item4.LastOrDefault(l => l.ParserName == "offset");

                                var limit = limitResult.Value as Expression ?? null;
                                var offset = offsetResult.Value as Expression ?? null;
                                var reversed = x.Item4.Any(l => l.ParserName == "reversed");

                                return new ForStatement(statements, identifier, range, limit, offset, reversed, null);
                            })
                        ).ElseError("Invalid 'for' tag");

            var KnownTags = Identifier.Switch((context, previous) =>
            {
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

            var tag = TagStart.SkipAnd(KnownTags.Or(OtherTags));

            var text = AnyCharBefore(OutputStart.Or(TagStart))
                .Then<Statement>(static x => new TextSpanStatement(x));

            TagsList.Parser = Star(Output.Or(tag).Or(text));

            Grammar = TagsList;
        }

        public static IParser<ValueTuple<string, string, string>> CreateTag(string tagName) => TagStart.And(Terms.Text(tagName)).And(TagEnd);

        public IFluidTemplate Parse(string template)
        {
            var success = Grammar.TryParse(template, out var statements, out var parlotError);

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
    }
}
