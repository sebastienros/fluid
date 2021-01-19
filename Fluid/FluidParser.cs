using Fluid.Ast;
using Fluid.Ast.BinaryExpressions;
using Fluid.Parser;
using Fluid.Values;
using Parlot;
using Parlot.Fluent;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using static Parlot.Fluent.Parsers;

namespace Fluid
{
    public class FluidParser
    {
        public Parser<List<Statement>> Grammar;
        public Dictionary<string, Parser<Statement>> RegisteredTags { get; } = new();

        protected static readonly Parser<char> LBrace = Terms.Char('{');
        protected static readonly Parser<char> RBrace = Terms.Char('}');
        protected static readonly Parser<char> LParen = Terms.Char('(');
        protected static readonly Parser<char> RParen = Terms.Char(')');
        protected static readonly Parser<char> LBracket = Literals.Char('[');
        protected static readonly Parser<char> RBracket = Terms.Char(']');
        protected static readonly Parser<char> Equal = Terms.Char('=');
        protected static readonly Parser<char> Colon = Terms.Char(':');
        protected static readonly Parser<char> Comma = Terms.Char(',');
        protected static readonly Parser<char> Dot = Literals.Char('.');
        protected static readonly Parser<char> Pipe = Terms.Char('|');

        protected static readonly Parser<TextSpan> String = Terms.String(StringLiteralQuotes.SingleOrDouble);
        protected static readonly Parser<decimal> Number = Terms.Decimal(NumberOptions.AllowSign);
        protected static readonly Parser<string> True = Terms.Text("true");
        protected static readonly Parser<string> False = Terms.Text("false");

        protected static readonly Parser<string> DoubleEquals = Terms.Text("==");
        protected static readonly Parser<string> NotEquals = Terms.Text("!=");
        protected static readonly Parser<string> Different = Terms.Text("<>");
        protected static readonly Parser<string> Greater = Terms.Text(">");
        protected static readonly Parser<string> Lower = Terms.Text("<");
        protected static readonly Parser<string> GreaterOr = Terms.Text(">=");
        protected static readonly Parser<string> LowerOr = Terms.Text("<=");
        protected static readonly Parser<string> Contains = Terms.Text("contains");
        protected static readonly Parser<string> BinaryOr = Terms.Text("or");
        protected static readonly Parser<string> BinaryAnd = Terms.Text("and");

        protected static readonly Parser<string> Identifier = Terms.Identifier(extraPart: static c => c == '-').Then(x => x.ToString());

        protected static readonly Deferred<Expression> Primary = Deferred<Expression>();
        protected static readonly Deferred<Expression> LogicalExpression = Deferred<Expression>();
        protected static readonly Deferred<Expression> FilterExpression = Deferred<Expression>();
        protected readonly Deferred<List<Statement>> KnownTagsList = Deferred<List<Statement>>();
        protected readonly Deferred<List<Statement>> AnyTagsList = Deferred<List<Statement>>();

        protected static readonly Parser<TagResult> OutputStart = TagParsers.OutputTagStart();
        protected static readonly Parser<TagResult> OutputEnd = TagParsers.OutputTagEnd(true);
        protected static readonly Parser<TagResult> TagStart = TagParsers.TagStart();
        protected static readonly Parser<TagResult> TagStartSpaced = TagParsers.TagStart(true);
        protected static readonly Parser<TagResult> TagEnd = TagParsers.TagEnd(true);

        public FluidParser()
        {
            var Integer = Terms.Integer().Then<Expression>(x => new LiteralExpression(NumberValue.Create(x)));

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
                Number.Then<Expression>(x => new LiteralExpression(NumberValue.Create(x)))
                .Or(String.Then<Expression>(x => new LiteralExpression(StringValue.Create(x.ToString()))))
                .Or(True.Then<Expression>(x => new LiteralExpression(BooleanValue.True)))
                .Or(False.Then<Expression>(x => new LiteralExpression(BooleanValue.False)))
                .Or(Member.Then<Expression>(x => x))
                ;

            var CaseValueList = Separated(BinaryOr, Primary);

            // TODO: 'and' has a higher priority than 'or', either create a new scope, or implement operators priority

            var Logical = Primary.And(ZeroOrMany(OneOf(BinaryOr, BinaryAnd, Contains).And(Primary)))
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

            var Comparison = Logical.And(ZeroOrMany(OneOf(DoubleEquals, NotEquals, Different, GreaterOr, LowerOr, Greater, Lower).And(Logical)))
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

            // Primary ( | identifer [ ':' ([name :] value ,)+ )! ] )*
            FilterExpression.Parser = Primary
                .And(ZeroOrMany(
                    Pipe
                    .SkipAnd(Identifier)
                    .And(ZeroOrOne(Colon.SkipAnd(
                        Separated(Comma,
                            OneOf(
                                Identifier.AndSkip(Colon).And(Primary).Then(static x => new FilterArgument(x.Item1.ToString(), x.Item2)),
                                Primary.Then(static x => new FilterArgument(null, x))
                            ))
                        )))
                    ))
                .Then(x =>
                    {
                        // Primary
                        var result = x.Item1;

                        // Filters
                        foreach (var pipeResult in x.Item2)
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


            var Text = AnyCharBefore(OutputStart.Or(TagStart))
                .Then<Statement>(static (ctx, x) =>
                {
                    // Keep track of each text span such that whitespace trimming can be applied

                    var p = (FluidParseContext)ctx;

                    var result = new TextSpanStatement(x);

                    p.PreviousTextSpanStatement = result;

                    if (p.StripNextTextSpanStatement)
                    {
                        result.StrippedLeft = true;
                        p.StripNextTextSpanStatement = false;
                    }
                    return result;
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
                        .And(AnyTagsList)
                        .AndSkip(CreateTag("endcapture"))
                        .Then<Statement>(x => new CaptureStatement(x.Item1.ToString(), x.Item2))
                        .ElseError("Invalid 'capture' tag")
                        ;
            var CycleTag = ZeroOrOne(Identifier.AndSkip(Colon))
                        .And(Separated(Comma, String))
                        .AndSkip(TagEnd)
                        .Then<Statement>(x =>
                        {
                            var group = string.IsNullOrEmpty(x.Item1)
                                ? null
                                : new LiteralExpression(StringValue.Create(x.Item1));

                            var values = x.Item2.Select(e => new LiteralExpression(StringValue.Create(e.ToString()))).ToList<Expression>();

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
                        .And(AnyTagsList)
                        .And(ZeroOrMany(
                            TagStart.SkipAnd(Terms.Text("elsif")).SkipAnd(LogicalExpression).AndSkip(TagEnd).And(AnyTagsList))
                            .Then(x => x.Select(e => new ElseIfStatement(e.Item1, e.Item2)).ToList()))
                        .And(ZeroOrOne(
                            CreateTag("else").SkipAnd(AnyTagsList))
                            .Then(x => x != null ? new ElseStatement(x) : null))
                        .AndSkip(CreateTag("endif"))
                        .Then<Statement>(x => new IfStatement(x.Item1, x.Item2, x.Item4, x.Item3))
                        .ElseError("Invalid 'if' tag");
            var UnlessTag = LogicalExpression
                        .AndSkip(TagEnd)
                        .And(AnyTagsList)
                        .AndSkip(CreateTag("endunless"))
                        .Then<Statement>(x => new UnlessStatement(x.Item1, x.Item2))
                        .ElseError("Invalid 'unless' tag");
            var CaseTag = Primary
                       .AndSkip(TagEnd)
                       .AndSkip(AnyCharBefore(TagStart, canBeEmpty: true))
                       .And(ZeroOrMany(
                           TagStart.Then(x => x).AndSkip(Terms.Text("when")).And(CaseValueList.ElseError("Invalid 'when' tag")).AndSkip(TagEnd).And(AnyTagsList))
                           .Then(x => x.Select(e => new WhenStatement(e.Item2, e.Item3)).ToArray()))
                       .And(ZeroOrOne(
                           CreateTag("else").SkipAnd(AnyTagsList))
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
                            .And(AnyTagsList)
                            .And(ZeroOrOne(
                                CreateTag("else").SkipAnd(AnyTagsList))
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
                            .And(AnyTagsList)
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

            RegisteredTags["break"] = BreakTag;
            RegisteredTags["continue"] = ContinueTag;
            RegisteredTags["comment"] = CommentTag;
            RegisteredTags["capture"] = CaptureTag;
            RegisteredTags["cycle"] = CycleTag;
            RegisteredTags["decrement"] = DecrementTag;
            RegisteredTags["include"] = IncludeTag;
            RegisteredTags["increment"] = IncrementTag;
            RegisteredTags["raw"] = RawTag;
            RegisteredTags["assign"] = AssignTag;
            RegisteredTags["if"] = IfTag;
            RegisteredTags["unless"] = UnlessTag;
            RegisteredTags["case"] = CaseTag;
            RegisteredTags["for"] = ForTag;

            var AnyTags = TagStart.SkipAnd(Identifier.ElseError("Expected tag name").Switch((context, previous) =>
            {
                // Because tags like 'else' are not listed, they won't count in TagsList, and will stop being processed 
                // as inner tags in blocks like {% if %} TagsList {% endif $}

                var tagName = previous.ToString();

                if (RegisteredTags.TryGetValue(tagName, out var tag))
                {
                    return tag;
                }
                else
                {
                    return null;
                }
            }));

            var KnownTags = TagStart.SkipAnd(Identifier.ElseError("Expected tag name").Switch((context, previous) =>
            {
                // Because tags like 'else' are not listed, they won't count in TagsList, and will stop being processed 
                // as inner tags in blocks like {% if %} TagsList {% endif $}

                var tagName = previous.ToString();

                if (RegisteredTags.TryGetValue(tagName, out var tag))
                {
                    return tag;
                }
                else
                {
                    throw new ParseException($"Unexpected tag '{tagName}'");
                }
            }));

            AnyTagsList.Parser = ZeroOrMany(Output.Or(AnyTags).Or(Text)); // Used in block and stop when an unknown tag is found
            KnownTagsList.Parser = ZeroOrMany(Output.Or(KnownTags).Or(Text)); // User in main list and raises an issue when an unknown tag is found

            Grammar = KnownTagsList;
        }

        public static Parser<string> CreateTag(string tagName) => TagStart.SkipAnd(Terms.Text(tagName)).AndSkip(TagEnd);

        public void RegisterEmptyTag(string tagName, Func<TextWriter, TextEncoder, TemplateContext, ValueTask<Completion>> render)
        {
            RegisteredTags[tagName] = TagEnd.Then<Statement>(x => new EmptyTagStatement(render));
        }

        public void RegisterIdentifierTag(string tagName, Func<string, TextWriter, TextEncoder, TemplateContext, ValueTask<Completion>> render)
        {
            RegisteredTags[tagName] = Identifier.AndSkip(TagEnd).Then<Statement>(x => new IdentifierTagStatement(x.ToString(), render));
        }

        public void RegisterEmptyBlock(string tagName, Func<IReadOnlyList<Statement>, TextWriter, TextEncoder, TemplateContext, ValueTask<Completion>> render)
        {
            RegisteredTags[tagName] = TagEnd.SkipAnd(AnyTagsList).AndSkip(CreateTag("end" + tagName)).Then<Statement>(x => new EmptyBlockStatement(x, render));
        }

        public void RegisterIdentifierBlock(string tagName, Func<string, IReadOnlyList<Statement>, TextWriter, TextEncoder, TemplateContext, ValueTask<Completion>> render)
        {
            RegisterParserBlock(tagName, Identifier, render);
        }

        public void RegisterExpressionBlock(string tagName, Func<Expression, IReadOnlyList<Statement>, TextWriter, TextEncoder, TemplateContext, ValueTask<Completion>> render)
        {
            RegisterParserBlock(tagName, Primary, render);
        }

        public void RegisterParserBlock<T>(string tagName, Parser<T> parser, Func<T, IReadOnlyList<Statement>, TextWriter, TextEncoder, TemplateContext, ValueTask<Completion>> render)
        {
            RegisteredTags[tagName] = parser.AndSkip(TagEnd).And(AnyTagsList).AndSkip(CreateTag("end" + tagName)).Then<Statement>(x => new ParserBlockStatement<T>(x.Item1, x.Item2, render));
        }
    }
}
