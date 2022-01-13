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
using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using static Parlot.Fluent.Parsers;

namespace Fluid
{
    public class FluidParser
    {
        public Parser<List<Statement>> Grammar;
        public Dictionary<string, Parser<Statement>> RegisteredTags { get; } = new();
        public Dictionary<string, Func<Expression, Expression, Expression>> RegisteredOperators { get; } = new();

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

        protected static readonly Parser<string> DoubleEquals = Terms.Text("==");
        protected static readonly Parser<string> NotEquals = Terms.Text("!=");
        protected static readonly Parser<string> Different = Terms.Text("<>");
        protected static readonly Parser<string> Greater = Terms.Text(">");
        protected static readonly Parser<string> Lower = Terms.Text("<");
        protected static readonly Parser<string> GreaterOr = Terms.Text(">=");
        protected static readonly Parser<string> LowerOr = Terms.Text("<=");
        protected static readonly Parser<string> Contains = Terms.Text("contains");
        protected static readonly Parser<string> StartsWith = Terms.Text("startswith");
        protected static readonly Parser<string> EndsWith = Terms.Text("endswith");
        protected static readonly Parser<string> BinaryOr = Terms.Text("or");
        protected static readonly Parser<string> BinaryAnd = Terms.Text("and");

        protected static readonly Parser<string> Identifier = SkipWhiteSpace(new IdentifierParser()).Then(x => x.ToString());

        protected readonly Parser<List<FilterArgument>> ArgumentsList;
        protected readonly Parser<List<FunctionCallArgument>> FunctionCallArgumentsList;
        protected readonly Parser<Expression> LogicalExpression;
        protected readonly Parser<Expression> CombinatoryExpression; // and | or
        protected readonly Deferred<Expression> Primary = Deferred<Expression>();
        protected readonly Deferred<Expression> FilterExpression = Deferred<Expression>();
        protected readonly Deferred<List<Statement>> KnownTagsList = Deferred<List<Statement>>();
        protected readonly Deferred<List<Statement>> AnyTagsList = Deferred<List<Statement>>();

        protected static readonly Parser<TagResult> OutputStart = TagParsers.OutputTagStart();
        protected static readonly Parser<TagResult> OutputEnd = TagParsers.OutputTagEnd(true);
        protected static readonly Parser<TagResult> TagStart = TagParsers.TagStart();
        protected static readonly Parser<TagResult> TagStartSpaced = TagParsers.TagStart(true);
        protected static readonly Parser<TagResult> TagEnd = TagParsers.TagEnd(true);

        protected static readonly LiteralExpression EmptyKeyword = new LiteralExpression(EmptyValue.Instance);
        protected static readonly LiteralExpression BlankKeyword = new LiteralExpression(BlankValue.Instance);
        protected static readonly LiteralExpression TrueKeyword = new LiteralExpression(BooleanValue.True);
        protected static readonly LiteralExpression FalseKeyword = new LiteralExpression(BooleanValue.False);

        public FluidParser() : this (new())
        {
        }
        
        public FluidParser(FluidParserOptions parserOptions) 
        {
            var Integer = Terms.Integer().Then<Expression>(x => new LiteralExpression(NumberValue.Create(x)));

            // Member expressions
            var Indexer = Between(LBracket, Primary, RBracket).Then<MemberSegment>(x => new IndexerSegment(x));

            // ([name =] value,)+
            FunctionCallArgumentsList = ZeroOrOne(Separated(Comma,
                            OneOf(
                                Identifier.AndSkip(Equal).And(Primary).Then(static x => new FunctionCallArgument(x.Item1, x.Item2)),
                                Primary.Then(static x => new FunctionCallArgument(null, x))
                            ))).Then(x => x ?? new List<FunctionCallArgument>());

            // (name [= value],)+
            var FunctionDefinitionArgumentsList = ZeroOrOne(Separated(Comma,
                            Identifier.And(ZeroOrOne(Equal.SkipAnd(Primary))).Then(static x => new FunctionCallArgument(x.Item1, x.Item2)))
                            ).Then(x => x ?? new List<FunctionCallArgument>());

            var Call = parserOptions.AllowFunctions
                ? LParen.SkipAnd(FunctionCallArgumentsList).AndSkip(RParen).Then<MemberSegment>(x => new FunctionCallSegment(x))
                : LParen.Error<MemberSegment>(ErrorMessages.FunctionsNotAllowed)
                ;

            var Member = Identifier.Then<MemberSegment>(x => new IdentifierSegment(x)).And(
                ZeroOrMany(
                    Dot.SkipAnd(Identifier.Then<MemberSegment>(x => new IdentifierSegment(x)))
                    .Or(Indexer)
                    .Or(Call)))
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
                .Then<Expression>(x => new RangeExpression(x.Item1, x.Item2));

            // primary => NUMBER | STRING | property
            Primary.Parser =
                String.Then<Expression>(x => new LiteralExpression(StringValue.Create(x)))
                .Or(Member.Then<Expression>(static x => {
                    if (x.Segments.Count == 1)
                    {
                        switch ((x.Segments[0] as IdentifierSegment).Identifier)
                        {
                            case "empty": return EmptyKeyword;
                            case "blank": return BlankKeyword;
                            case "true": return TrueKeyword;
                            case "false": return FalseKeyword;
                        }
                    }

                    return x;
                }))
                .Or(Number.Then<Expression>(x => new LiteralExpression(NumberValue.Create(x))))
                .Or(Range)
                ;

            RegisteredOperators["contains"] = (a, b) => new ContainsBinaryExpression(a, b);
            RegisteredOperators["startswith"] = (a, b) => new StartsWithBinaryExpression(a, b);
            RegisteredOperators["endswith"] = (a, b) => new EndsWithBinaryExpression(a, b);
            RegisteredOperators["=="] = (a, b) => new EqualBinaryExpression(a, b);
            RegisteredOperators["!="] = (a, b) => new NotEqualBinaryExpression(a, b);
            RegisteredOperators["<>"] = (a, b) => new NotEqualBinaryExpression(a, b);
            RegisteredOperators[">"] = (a, b) => new GreaterThanBinaryExpression(a, b, true);
            RegisteredOperators["<"] = (a, b) => new LowerThanExpression(a, b, true);
            RegisteredOperators[">="] = (a, b) => new GreaterThanBinaryExpression(a, b, false);
            RegisteredOperators["<="] = (a, b) => new LowerThanExpression(a, b, false);

            var CaseValueList = Separated(BinaryOr, Primary);

            CombinatoryExpression = Primary.And(ZeroOrOne(OneOf(Terms.Pattern(x => x == '=' || x == '!' || x == '<' || x == '>', maxSize: 2), Terms.Identifier().AndSkip(Literals.WhiteSpace())).Then(x => x.ToString()).When(x => RegisteredOperators.ContainsKey(x)).And(Primary)))
                .Then(x =>
                 {
                     if (x.Item2.Item1 == null)
                     {
                         return x.Item1;
                     }

                     return RegisteredOperators[x.Item2.Item1](x.Item1, x.Item2.Item2);
                 });

            LogicalExpression = CombinatoryExpression.And(ZeroOrMany(OneOf(Terms.Text("or"), Terms.Text("and")).And(CombinatoryExpression)))
                .Then(x =>
                {
                    if (x.Item2.Count == 0)
                    {
                        return x.Item1;
                    }

                    var result = x.Item2[x.Item2.Count - 1].Item2;

                    for (var i = x.Item2.Count - 1; i >= 0; i--)
                    {
                        var current = x.Item2[i];
                        var previous = i == 0 ? x.Item1 : x.Item2[i - 1].Item2;

                        result = current.Item1 switch
                        {
                            "or" => new OrBinaryExpression(previous, result),
                            "and" => new AndBinaryExpression(previous, result),
                            _ => throw new ParseException()
                        };
                        
                    }

                    return result;
                });

            // ([name :] value ,)+
            ArgumentsList = Separated(Comma,
                            OneOf(
                                Identifier.AndSkip(Colon).And(Primary).Then(static x => new FilterArgument(x.Item1, x.Item2)),
                                Primary.Then(static x => new FilterArgument(null, x))
                            ));

            // Primary ( | identifer ( ':' ArgumentsList )! ] )*
            FilterExpression.Parser = LogicalExpression.ElseError(ErrorMessages.LogicalExpressionStartsFilter)
                .And(ZeroOrMany(
                    Pipe
                    .SkipAnd(Identifier.ElseError(ErrorMessages.IdentifierAfterPipe))
                    .And(ZeroOrOne(Colon.SkipAnd(ArgumentsList)))))
                .Then((ctx, x) =>
                    {
                        // Primary
                        var result = x.Item1;

                        // Filters
                        foreach (var pipeResult in x.Item2)
                        {
                            var identifier = pipeResult.Item1;
                            var arguments = pipeResult.Item2;

                            result = new FilterExpression(result, identifier, arguments);
                        }

                        return result;
                    });

            var Output = OutputStart.SkipAnd(FilterExpression.And(OutputEnd.ElseError(ErrorMessages.ExpectedOutputEnd))
                .Then<Statement>(static x => new OutputStatement(x.Item1))
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
                        result.StripLeft = true;
                        p.StripNextTextSpanStatement = false;
                    }

                    result.PreviousIsTag = p.PreviousIsTag;
                    result.PreviousIsOutput = p.PreviousIsOutput;

                    return result;
                });


            var BreakTag = TagEnd.Then<Statement>(x => new BreakStatement()).ElseError("Invalid 'break' tag");
            var ContinueTag = TagEnd.Then<Statement>(x => new ContinueStatement()).ElseError("Invalid 'continue' tag");
            var CommentTag = TagEnd
                        .SkipAnd(AnyCharBefore(CreateTag("endcomment")))
                        .AndSkip(CreateTag("endcomment").ElseError($"'{{% endcomment %}}' was expected"))
                        .Then<Statement>(x => new CommentStatement(x))
                        .ElseError("Invalid 'comment' tag")
                        ;
            var CaptureTag = Identifier
                        .AndSkip(TagEnd)
                        .And(AnyTagsList)
                        .AndSkip(CreateTag("endcapture").ElseError($"'{{% endcapture %}}' was expected"))
                        .Then<Statement>(x => new CaptureStatement(x.Item1, x.Item2))
                        .ElseError("Invalid 'capture' tag")
                        ;
            var MacroTag = Identifier.ElseError(ErrorMessages.IdentifierAfterMacro)
                        .AndSkip(LParen).ElseError(ErrorMessages.IdentifierAfterMacro)
                        .And(FunctionDefinitionArgumentsList)
                        .AndSkip(RParen)
                        .AndSkip(TagEnd)
                        .And(AnyTagsList)
                        .AndSkip(CreateTag("endmacro").ElseError($"'{{% endmacro %}}' was expected"))
                        .Then<Statement>(x => new MacroStatement(x.Item1, x.Item2, x.Item3))
                        .ElseError("Invalid 'macro' tag")
                        ;
            var CycleTag = ZeroOrOne(Primary.AndSkip(Colon))
                        .And(Separated(Comma, Primary))
                        .AndSkip(TagEnd)
                        .Then<Statement>(x => new CycleStatement(x.Item1, x.Item2))
                        .ElseError("Invalid 'cycle' tag")
                        ;
            var DecrementTag = ZeroOrOne(Identifier).AndSkip(TagEnd)
                        .Then<Statement>(x => new DecrementStatement(x))
                        .ElseError("Invalid 'decrement' tag")
                        ;
            var IncrementTag = ZeroOrOne(Identifier).AndSkip(TagEnd)
                        .Then<Statement>(x => new IncrementStatement(x))
                        .ElseError("Invalid 'increment' tag")
                        ;

            var IncludeTag = OneOf(
                        Primary.AndSkip(Comma).And(Separated(Comma, Identifier.AndSkip(Colon).And(Primary).Then(static x => new AssignStatement(x.Item1, x.Item2)))).Then(x => new IncludeStatement(this, x.Item1, null, null, null, x.Item2)),
                        Primary.AndSkip(Terms.Text("with")).And(Primary).And(ZeroOrOne(Terms.Text("as").SkipAnd(Identifier))).Then(x => new IncludeStatement(this, x.Item1, with: x.Item2, alias: x.Item3)),
                        Primary.AndSkip(Terms.Text("for")).And(Primary).And(ZeroOrOne(Terms.Text("as").SkipAnd(Identifier))).Then(x => new IncludeStatement(this, x.Item1, @for: x.Item2, alias: x.Item3)),
                        Primary.Then(x => new IncludeStatement(this, x))
                        ).AndSkip(TagEnd)
                        .Then<Statement>(x => x)
                        .ElseError("Invalid 'include' tag")
                        ;

            var StringAfterRender = String.ElseError(ErrorMessages.ExpectedStringRender);

            var RenderTag = OneOf(
                        StringAfterRender.AndSkip(Comma).And(Separated(Comma, Identifier.AndSkip(Colon).And(Primary).Then(static x => new AssignStatement(x.Item1, x.Item2)))).Then(x => new RenderStatement(this, x.Item1.ToString(), null, null, null, x.Item2)),
                        StringAfterRender.AndSkip(Terms.Text("with")).And(Primary).And(ZeroOrOne(Terms.Text("as").SkipAnd(Identifier))).Then(x => new RenderStatement(this, x.Item1.ToString(), with: x.Item2, alias: x.Item3)),
                        StringAfterRender.AndSkip(Terms.Text("for")).And(Primary).And(ZeroOrOne(Terms.Text("as").SkipAnd(Identifier))).Then(x => new RenderStatement(this, x.Item1.ToString(), @for: x.Item2, alias: x.Item3)),
                        StringAfterRender.Then(x => new RenderStatement(this, x.ToString()))
                        ).AndSkip(TagEnd)
                        .Then<Statement>(x => x)
                        .ElseError("Invalid 'render' tag")
                        ;

            var RawTag = TagEnd.SkipAnd(AnyCharBefore(CreateTag("endraw"), consumeDelimiter: true, failOnEof: true).Then<Statement>(x => new RawStatement(x))).ElseError("Not end tag found for {% raw %}");
            var AssignTag = Identifier.Then(x => x).ElseError(ErrorMessages.IdentifierAfterAssign).AndSkip(Equal.ElseError(ErrorMessages.EqualAfterAssignIdentifier)).And(FilterExpression).AndSkip(TagEnd.ElseError(ErrorMessages.ExpectedTagEnd)).Then<Statement>(x => new AssignStatement(x.Item1, x.Item2));
            var IfTag = LogicalExpression
                        .AndSkip(TagEnd)
                        .And(AnyTagsList)
                        .And(ZeroOrMany(
                            TagStart.SkipAnd(Terms.Text("elsif")).SkipAnd(LogicalExpression).AndSkip(TagEnd).And(AnyTagsList))
                            .Then(x => x.Select(e => new ElseIfStatement(e.Item1, e.Item2)).ToList()))
                        .And(ZeroOrOne(
                            CreateTag("else").SkipAnd(AnyTagsList))
                            .Then(x => x != null ? new ElseStatement(x) : null))
                        .AndSkip(CreateTag("endif").ElseError($"'{{% endif %}}' was expected"))
                        .Then<Statement>(x => new IfStatement(x.Item1, x.Item2, x.Item4, x.Item3))
                        .ElseError("Invalid 'if' tag");
            var UnlessTag = LogicalExpression
                        .AndSkip(TagEnd)
                        .And(AnyTagsList)
                        .And(ZeroOrOne(
                            CreateTag("else").SkipAnd(AnyTagsList))
                            .Then(x => x != null ? new ElseStatement(x) : null))
                        .AndSkip(CreateTag("endunless").ElseError($"'{{% endunless %}}' was expected"))
                        .Then<Statement>(x => new UnlessStatement(x.Item1, x.Item2, x.Item3))
                        .ElseError("Invalid 'unless' tag");
            var CaseTag = Primary
                       .AndSkip(TagEnd)
                       .AndSkip(AnyCharBefore(TagStart, canBeEmpty: true))
                       .And(ZeroOrMany(
                           TagStart.AndSkip(Terms.Text("when")).And(CaseValueList.ElseError("Invalid 'when' tag")).AndSkip(TagEnd).And(AnyTagsList))
                           .Then(x => x.Select(e => new WhenStatement(e.Item2, e.Item3)).ToArray()))
                       .And(ZeroOrOne(
                           CreateTag("else").SkipAnd(AnyTagsList))
                           .Then(x => x != null ? new ElseStatement(x) : null))
                       .AndSkip(CreateTag("endcase").ElseError($"'{{% endcase %}}' was expected"))
                       .Then<Statement>(x => new CaseStatement(x.Item1, x.Item3, x.Item2))
                       .ElseError("Invalid 'case' tag");
            var ForTag = OneOf(
                            Identifier
                            .AndSkip(Terms.Text("in"))
                            .And(Member)
                            .And(ZeroOrMany(OneOf( // Use * since each can appear in any order. Validation is done once it's parsed
                                Terms.Text("reversed").Then(x => new ForModifier { IsReversed = true }),
                                Terms.Text("limit").SkipAnd(Colon).SkipAnd(Primary).Then(x => new ForModifier { IsLimit = true, Value = x }),
                                Terms.Text("offset").SkipAnd(Colon).SkipAnd(Primary).Then(x => new ForModifier { IsOffset = true, Value = x })
                                )))
                            .AndSkip(TagEnd)
                            .And(AnyTagsList)
                            .And(ZeroOrOne(
                                CreateTag("else").SkipAnd(AnyTagsList))
                                .Then(x => x != null ? new ElseStatement(x) : null))
                            .AndSkip(CreateTag("endfor").ElseError($"'{{% endfor %}}' was expected"))
                            .Then<Statement>(x =>
                            {
                                var identifier = x.Item1;
                                var member = x.Item2;
                                var statements = x.Item4;
                                var elseStatement = x.Item5;
                                var (limitResult, offsetResult, reversed) = ReadForStatementConfiguration(x.Item3);
                                return new ForStatement(statements, identifier, member, limitResult, offsetResult, reversed, elseStatement);
                            }),

                            Identifier
                            .AndSkip(Terms.Text("in"))
                            .And(Range)
                            .And(ZeroOrMany(OneOf( // Use * since each can appear in any order. Validation is done once it's parsed
                                Terms.Text("reversed").Then(x => new ForModifier { IsReversed = true }),
                                Terms.Text("limit").SkipAnd(Colon).SkipAnd(Primary).Then(x => new ForModifier { IsLimit = true, Value = x }),
                                Terms.Text("offset").SkipAnd(Colon).SkipAnd(Primary).Then(x => new ForModifier { IsOffset = true, Value = x })
                                )))
                            .AndSkip(TagEnd)
                            .And(AnyTagsList)
                            .AndSkip(CreateTag("endfor").ElseError($"'{{% endfor %}}' was expected"))
                            .Then<Statement>(x =>
                            {
                                var identifier = x.Item1;
                                var range = x.Item2;
                                var statements = x.Item4;
                                var (limitResult, offsetResult, reversed) = ReadForStatementConfiguration(x.Item3);
                                return new ForStatement(statements, identifier, range, limitResult, offsetResult, reversed, null);

                            })
                        ).ElseError("Invalid 'for' tag");

            var LiquidTag = Literals.WhiteSpace(true) // {% liquid %} can start with new lines
                .Then((context, x) => { ((FluidParseContext)context).InsideLiquidTag = true; return x;})
                .SkipAnd(OneOrMany(Identifier.Switch((context, previous) =>
            {
                // Because tags like 'else' are not listed, they won't count in TagsList, and will stop being processed
                // as inner tags in blocks like {% if %} TagsList {% endif $}

                var tagName = previous;

                if (RegisteredTags.TryGetValue(tagName, out var tag))
                {
                    return tag;
                }
                else
                {
                    throw new ParseException($"Unknown tag '{tagName}' at {context.Scanner.Cursor.Position}");
                }
            })))
                .Then((context, x) => { ((FluidParseContext)context).InsideLiquidTag = false; return x; })
                .AndSkip(TagEnd).Then<Statement>(x => new LiquidStatement(x))
                ;

            var EchoTag = FilterExpression.AndSkip(TagEnd).Then<Statement>(x => new OutputStatement(x));

            RegisteredTags["break"] = BreakTag;
            RegisteredTags["continue"] = ContinueTag;
            RegisteredTags["comment"] = CommentTag;
            RegisteredTags["capture"] = CaptureTag;
            RegisteredTags["cycle"] = CycleTag;
            RegisteredTags["decrement"] = DecrementTag;
            RegisteredTags["include"] = IncludeTag;
            RegisteredTags["render"] = RenderTag;
            RegisteredTags["increment"] = IncrementTag;
            RegisteredTags["raw"] = RawTag;
            RegisteredTags["assign"] = AssignTag;
            RegisteredTags["if"] = IfTag;
            RegisteredTags["unless"] = UnlessTag;
            RegisteredTags["case"] = CaseTag;
            RegisteredTags["for"] = ForTag;
            RegisteredTags["liquid"] = LiquidTag;
            RegisteredTags["echo"] = EchoTag;

            if (parserOptions.AllowFunctions)
            {
                RegisteredTags["macro"] = MacroTag;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static (Expression limitResult, Expression offsetResult, bool reversed) ReadForStatementConfiguration(List<ForModifier> modifiers)
            {
                if (modifiers.Count == 0)
                {
                    return (null, null, false);
                }

                // take slower route when needed
                static (Expression limitResult, Expression offsetResult, bool reversed) ReadFromList(List<ForModifier> modifiers)
                {
                    Expression limitResult = null;
                    Expression offsetResult = null;
                    var reversed = false;
                    for (var i = modifiers.Count - 1; i > -1; --i)
                    {
                        var l = modifiers[i];
                        if (l.IsLimit && limitResult is null)
                        {
                            limitResult = l.Value;
                        }

                        if (l.IsOffset && offsetResult is null)
                        {
                            offsetResult = l.Value;
                        }

                        reversed |= l.IsReversed;
                    }

                    return (limitResult, offsetResult, reversed);
                }


                return ReadFromList(modifiers);
            }

            var AnyTags = TagStart.SkipAnd(Identifier.ElseError(ErrorMessages.IdentifierAfterTagStart).Switch((context, previous) =>
            {
                // Because tags like 'else' are not listed, they won't count in TagsList, and will stop being processed
                // as inner tags in blocks like {% if %} TagsList {% endif $}

                var tagName = previous;

                if (RegisteredTags.TryGetValue(tagName, out var tag))
                {
                    return tag;
                }
                else
                {
                    return null;
                }
            }));

            var KnownTags = TagStart.SkipAnd(Identifier.ElseError(ErrorMessages.IdentifierAfterTagStart).Switch((context, previous) =>
            {
                // Because tags like 'else' are not listed, they won't count in TagsList, and will stop being processed
                // as inner tags in blocks like {% if %} TagsList {% endif $}

                var tagName = previous;

                if (RegisteredTags.TryGetValue(tagName, out var tag))
                {
                    return tag;
                }
                else
                {
                    throw new ParseException($"Unknown tag '{tagName}' at {context.Scanner.Cursor.Position}");
                }
            }));

            AnyTagsList.Parser = ZeroOrMany(Output.Or(AnyTags).Or(Text)); // Used in block and stop when an unknown tag is found
            KnownTagsList.Parser = ZeroOrMany(Output.Or(KnownTags).Or(Text)); // Used in main list and raises an issue when an unknown tag is found

            Grammar = KnownTagsList;
        }

        public static Parser<string> CreateTag(string tagName) => TagStart.SkipAnd(Terms.Text(tagName)).AndSkip(TagEnd);

        public void RegisterIdentifierTag(string tagName, Func<string, TextWriter, TextEncoder, TemplateContext, ValueTask<Completion>> render)
        {
            RegisterParserTag(tagName, Identifier.ElseError($"An identifier was expected after the '{tagName}' tag"), render);
        }

        public void RegisterIdentifierBlock(string tagName, Func<string, IReadOnlyList<Statement>, TextWriter, TextEncoder, TemplateContext, ValueTask<Completion>> render)
        {
            RegisterParserBlock(tagName, Identifier.ElseError($"An identifier was expected after the '{tagName}' tag"), render);
        }

        public void RegisterExpressionBlock(string tagName, Func<Expression, IReadOnlyList<Statement>, TextWriter, TextEncoder, TemplateContext, ValueTask<Completion>> render)
        {
            RegisterParserBlock(tagName, FilterExpression, render);
        }

        public void RegisterExpressionTag(string tagName, Func<Expression, TextWriter, TextEncoder, TemplateContext, ValueTask<Completion>> render)
        {
            RegisterParserTag(tagName, FilterExpression, render);
        }

        public void RegisterParserBlock<T>(string tagName, Parser<T> parser, Func<T, IReadOnlyList<Statement>, TextWriter, TextEncoder, TemplateContext, ValueTask<Completion>> render)
        {
            RegisteredTags[tagName] = parser.AndSkip(TagEnd).And(AnyTagsList).AndSkip(CreateTag("end" + tagName).ElseError($"'{{% end{tagName} %}}' was expected"))
                .Then<Statement>(x => new ParserBlockStatement<T>(x.Item1, x.Item2, render))
                .ElseError($"Invalid {tagName} tag")
                ;
        }

        public void RegisterParserTag<T>(string tagName, Parser<T> parser, Func<T, TextWriter, TextEncoder, TemplateContext, ValueTask<Completion>> render)
        {
            RegisteredTags[tagName] = parser.AndSkip(TagEnd).Then<Statement>(x => new ParserTagStatement<T>(x, render));
        }

        public void RegisterEmptyTag(string tagName, Func<TextWriter, TextEncoder, TemplateContext, ValueTask<Completion>> render)
        {
            RegisteredTags[tagName] = TagEnd.Then<Statement>(x => new EmptyTagStatement(render)).ElseError($"Unexpected arguments in {tagName} tag");
        }

        public void RegisterEmptyBlock(string tagName, Func<IReadOnlyList<Statement>, TextWriter, TextEncoder, TemplateContext, ValueTask<Completion>> render)
        {
            RegisteredTags[tagName] = TagEnd.SkipAnd(AnyTagsList).AndSkip(CreateTag("end" + tagName).ElseError($"'{{% end{tagName} %}}' was expected"))
                .Then<Statement>(x => new EmptyBlockStatement(x, render))
                .ElseError($"Invalid '{tagName}' tag")
                ;
        }

        /// <summary>
        /// Compiles all expressions.
        /// </summary>
        public virtual FluidParser Compile()
        {
            foreach (var entry in RegisteredTags.ToArray())
            {
                RegisteredTags[entry.Key] = entry.Value.Compile();
            }

            Grammar = Grammar.Compile();

            return this;
        }
    }
}
