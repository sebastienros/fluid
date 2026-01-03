using Fluid.Ast;
using Fluid.Ast.BinaryExpressions;
using Fluid.Parser;
using Fluid.Values;
using Parlot;
using Parlot.Fluent;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using static Parlot.Fluent.Parsers;

namespace Fluid
{
    public class FluidParser
    {
        public Parser<IReadOnlyList<Statement>> Grammar;
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

        protected readonly Parser<string> Identifier;

        protected readonly Parser<IReadOnlyList<FilterArgument>> ArgumentsList;
        protected readonly Parser<IReadOnlyList<FunctionCallArgument>> FunctionCallArgumentsList;
        protected readonly Parser<Expression> LogicalExpression;
        protected readonly Parser<Expression> CombinatoryExpression; // and | or
        protected readonly Deferred<Expression> Primary = Deferred<Expression>();
        protected readonly Deferred<Expression> FilterExpression = Deferred<Expression>();
        protected readonly Deferred<IReadOnlyList<Statement>> KnownTagsList = Deferred<IReadOnlyList<Statement>>();
        protected readonly Deferred<IReadOnlyList<Statement>> AnyTagsList = Deferred<IReadOnlyList<Statement>>();

        internal const string WhiteSpaceChars = "\t\n\v\f\r \u0085             \u2028\u2029  　";

        protected static readonly Parser<TagResult> InlineOutputStart = TagParsers.OutputTagStart();
        protected static readonly Parser<TagResult> InlineOutputEnd = TagParsers.OutputTagEnd();
        protected static readonly Parser<TagResult> InlineTagStart = TagParsers.TagStart();
        protected static readonly Parser<TagResult> InlineTagEnd = TagParsers.TagEnd();

        protected static readonly Parser<TagResult> NoInlineOutputStart = NonInlineLiquidTagParsers.OutputTagStart();
        protected static readonly Parser<TagResult> NoInlineOutputEnd = Literals.AnyOf(WhiteSpaceChars.AsSpan(), minSize: 0).SkipAnd(NonInlineLiquidTagParsers.OutputTagEnd());
        protected static readonly Parser<TagResult> NoInlineTagStart = NonInlineLiquidTagParsers.TagStart();
        protected static readonly Parser<TagResult> NoInlineTagEnd = Literals.AnyOf(WhiteSpaceChars.AsSpan(), minSize: 0).SkipAnd(NonInlineLiquidTagParsers.TagEnd());

        protected readonly Parser<TagResult> OutputStart = InlineOutputStart;
        protected readonly Parser<TagResult> OutputEnd = InlineOutputEnd;
        protected readonly Parser<TagResult> TagStart = InlineTagStart;
        protected readonly Parser<TagResult> TagEnd = InlineTagEnd;

        protected static readonly Parser<TagResult> RawOutputStart = NonInlineLiquidTagParsers.OutputTagStart();
        protected static readonly Parser<TagResult> RawTagStart = NonInlineLiquidTagParsers.TagStart();

        protected static readonly LiteralExpression EmptyKeyword = new LiteralExpression(EmptyValue.Instance);
        protected static readonly LiteralExpression BlankKeyword = new LiteralExpression(BlankValue.Instance);
        protected static readonly LiteralExpression NilKeyword = new LiteralExpression(NilValue.Instance);
        protected static readonly LiteralExpression TrueKeyword = new LiteralExpression(BooleanValue.True);
        protected static readonly LiteralExpression FalseKeyword = new LiteralExpression(BooleanValue.False);

        public FluidParser() : this(new())
        {
        }

        public FluidParser(FluidParserOptions parserOptions)
        {
            if (!parserOptions.AllowLiquidTag)
            {
                OutputStart = NoInlineOutputStart;
                OutputEnd = NoInlineOutputEnd;
                TagStart = NoInlineTagStart;
                TagEnd = NoInlineTagEnd;
            }

            Identifier = SkipWhiteSpace(new IdentifierParser(parserOptions.AllowTrailingQuestionMark)).Then(x => x.ToString());

            String.Name = "String";

            var Integer = Terms.Integer().Then<Expression>(x => new LiteralExpression(NumberValue.Create(x)));
            Integer.Name = "Integer";

            var Number = Terms.Decimal().Then<Expression>(x => new LiteralExpression(NumberValue.Create(x)));
            Number.Name = "Number";

            // Member expressions
            var Indexer = Between(LBracket, Primary, RBracket).Then<MemberSegment>(x => new IndexerSegment(x));
            Indexer.Name = "Indexer";

            // ([name =] value,)+
            FunctionCallArgumentsList = ZeroOrOne(Separated(Comma,
                            OneOf(
                                Identifier.AndSkip(Equal).And(Primary).Then(static x => new FunctionCallArgument(x.Item1, x.Item2)),
                                Primary.Then(static x => new FunctionCallArgument(null, x))
                            )));
            FunctionCallArgumentsList.Name = "FunctionArgumentsList";

            // (name [= value],)+
            var FunctionDefinitionArgumentsList = ZeroOrOne(Separated(Comma,
                            Identifier.And(ZeroOrOne(Equal.SkipAnd(Primary))).Then(static x => new FunctionCallArgument(x.Item1, x.Item2))));
            FunctionDefinitionArgumentsList.Name = "FunctionDefinitionArgumentsList";

            var Call = parserOptions.AllowFunctions
                ? LParen.SkipAnd(FunctionCallArgumentsList).AndSkip(RParen).Then<MemberSegment>(x => new FunctionCallSegment(x))
                : LParen.Error<MemberSegment>(ErrorMessages.FunctionsNotAllowed)
                ;
            Call.Name = "Call";

            // An Identifier followed by a list of MemberSegments (dot accessor, indexer or arguments list)
            var Member = Identifier.Then<MemberSegment>(x => new IdentifierSegment(x)).And(
                ZeroOrMany(
                    Dot.SkipAnd(
                        Identifier.Or(Terms.Integer(NumberOptions.None).Then(x => x.ToString(CultureInfo.InvariantCulture)))
                            .Then<MemberSegment>(x => new IdentifierSegment(x))
                    )
                    .Or(Indexer)
                    .Or(Call)))
                .Then(x => new MemberExpression([x.Item1, .. x.Item2]));
            Member.Name = "Member";

            var Range = LParen
                .SkipAnd(OneOf(
                    Member.AndSkip(Terms.Text("..")),
                    Integer.AndSkip(Terms.Text("..")),
                    Number.AndSkip(Terms.Text(".."))
                ))
                .And(OneOf(Number, Member.Then<Expression>(x => x)))
                .AndSkip(RParen)
                .Then<Expression>(x => new RangeExpression(x.Item1, x.Item2));
            Range.Name = "Range";

            var Group = parserOptions.AllowParentheses
                ? LParen.SkipAnd(FilterExpression).AndSkip(RParen)
                : LParen.SkipAnd(FilterExpression).AndSkip(RParen).Error<Expression>(ErrorMessages.ParenthesesNotAllowed)
                ;
            Group.Name = "Group";

            // primary => NUMBER | STRING | property
            Primary.Parser = 
                String.Then<Expression>(x => new LiteralExpression(StringValue.Create(x)))
                .Or(Member.Then<Expression>(static x =>
                {
                    if (x.Segments.Count == 1)
                    {
                        switch ((x.Segments[0] as IdentifierSegment).Identifier)
                        {
                            case "empty": return EmptyKeyword;
                            case "blank": return BlankKeyword;
                            case "nil": return NilKeyword; // Both nil and null are supported for convenience
                            case "null": return NilKeyword;
                            case "true": return TrueKeyword;
                            case "false": return FalseKeyword;
                        }
                    }

                    return x;
                }))
                .Or(Number)
                .Or(Range)
                .Or(Group)
            ;
            Primary.Name = "Primary";

            RegisteredOperators["contains"] = (a, b) => new ContainsBinaryExpression(a, b);
            RegisteredOperators["startswith"] = (a, b) => new StartsWithBinaryExpression(a, b);
            RegisteredOperators["endswith"] = (a, b) => new EndsWithBinaryExpression(a, b);
            RegisteredOperators["=="] = (a, b) => new EqualBinaryExpression(a, b);
            RegisteredOperators["!="] = (a, b) => new NotEqualBinaryExpression(a, b);
            RegisteredOperators["<>"] = (a, b) => new NotEqualBinaryExpression(a, b);
            RegisteredOperators[">"] = (a, b) => new GreaterThanBinaryExpression(a, b, true);
            RegisteredOperators["<"] = (a, b) => new LowerThanBinaryExpression(a, b, true);
            RegisteredOperators[">="] = (a, b) => new GreaterThanBinaryExpression(a, b, false);
            RegisteredOperators["<="] = (a, b) => new LowerThanBinaryExpression(a, b, false);

            var CaseValueList = Separated(Terms.Text("or").Or(Terms.Text(",")), Primary);
            CaseValueList.Name = "CaseValueList";

            // Seek anything that looks like a binary operator (==, !=, <, >, <=, >=, contains, startswith, endswith) then validates it with the registered operators
            // An "identifier" operator should always be followed by a space so we ensure it's doing it with AndSkip(Literals.WhiteSpace())
            CombinatoryExpression = Primary.And(ZeroOrOne(OneOf(Terms.AnyOf("=!<>".AsSpan(), maxSize: 2), Terms.Identifier().AndSkip(Literals.WhiteSpace())).Then(x => x.ToString())
                .When((ctx, s) => RegisteredOperators.ContainsKey(s)).And(Primary)))
                .Then(x =>
                 {
                     if (x.Item2.Item1 == null)
                     {
                         return x.Item1;
                     }

                     return RegisteredOperators[x.Item2.Item1](x.Item1, x.Item2.Item2);
                 }).Named("CombinatoryExpression");

            LogicalExpression = CombinatoryExpression.And(ZeroOrMany(OneOf(Terms.Text("or"), Terms.Text("and")).And(CombinatoryExpression)))
                .Then(x =>
                {
                    if (x.Item2.Count == 0)
                    {
                        return x.Item1;
                    }

                    var result = x.Item2[^1].Item2;

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
            LogicalExpression.Name = "LogicalExpression";

            // ([name :] value ,)+
            ArgumentsList = Separated(Comma,
                            OneOf(
                                Identifier.AndSkip(Colon).And(Primary).Then(static x => new FilterArgument(x.Item1, x.Item2)),
                                Primary.Then(static x => new FilterArgument(null, x))
                            ));
            ArgumentsList.Name = "ArgumentsList";

            // Primary ( | identifier ( ':' ArgumentsList )! ] )*
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
            FilterExpression.Name = "FilterExpression";

            var Output = OutputStart.SkipAnd(FilterExpression.And(OutputEnd.ElseError(ErrorMessages.ExpectedOutputEnd))
                .Then<Statement>(static x => new OutputStatement(x.Item1))
                );
            Output.Name = "Output";

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
            Text.Name = "Text";

            var BreakTag = TagEnd.Then<Statement>(x => new BreakStatement()).ElseError("Invalid 'break' tag");
            BreakTag.Name = "BreakTag";

            var ContinueTag = TagEnd.Then<Statement>(x => new ContinueStatement()).ElseError("Invalid 'continue' tag");
            ContinueTag.Name = "ContinueTag";

            var CommentTag = TagEnd
                        .SkipAnd(AnyCharBefore(CreateTag("endcomment"), canBeEmpty: true))
                        .AndSkip(CreateTag("endcomment").ElseError($"'{{% endcomment %}}' was expected"))
                        .Then<Statement>(x => new CommentStatement(x))
                        .ElseError("Invalid 'comment' tag")
                        ;
            CommentTag.Name = "CommentTag";

            var InlineCommentTag = AnyCharBefore(TagEnd, canBeEmpty: true)
                        .AndSkip(TagEnd)
                        .Then<Statement>(x => new CommentStatement(x))
                        .ElseError("Invalid inline comment tag")
                        ;
            InlineCommentTag.Name = "InlineCommentTag";

            var DocTag = TagEnd
                        .SkipAnd(AnyCharBefore(CreateTag("enddoc"), canBeEmpty: true))
                        .AndSkip(CreateTag("enddoc").ElseError($"'{{% enddoc %}}' was expected"))
                        .Then<Statement>(x => new DocStatement(x))
                        .ElseError("Invalid 'doc' tag")
                        ;
            DocTag.Name = "DocTag";

            var CaptureTag = Identifier.ElseError(string.Format(ErrorMessages.IdentifierAfterTag, "capture"))
                        .AndSkip(TagEnd)
                        .And(AnyTagsList)
                        .AndSkip(CreateTag("endcapture").ElseError($"'{{% endcapture %}}' was expected"))
                        .Then<Statement>(x => new CaptureStatement(x.Item1, x.Item2))
                        .ElseError("Invalid 'capture' tag")
                        ;
            CaptureTag.Name = "CaptureTag";

            var MacroTag = Identifier.ElseError(string.Format(ErrorMessages.IdentifierAfterTag, "macro"))
                        .AndSkip(LParen).ElseError(string.Format(ErrorMessages.IdentifierAfterTag, "macro"))
                        .And(FunctionDefinitionArgumentsList)
                        .AndSkip(RParen)
                        .AndSkip(TagEnd)
                        .And(AnyTagsList)
                        .AndSkip(CreateTag("endmacro").ElseError($"'{{% endmacro %}}' was expected"))
                        .Then<Statement>(x => new MacroStatement(x.Item1, x.Item2, x.Item3))
                        .ElseError("Invalid 'macro' tag")
                        ;
            MacroTag.Name = "MacroTag";

            var CycleTag = ZeroOrOne(Primary.AndSkip(Colon))
                        .And(Separated(Comma, Primary))
                        .AndSkip(TagEnd)
                        .Then<Statement>(x => new CycleStatement(x.Item1, x.Item2))
                        .ElseError("Invalid 'cycle' tag")
                        ;
            CycleTag.Name = "CycleTag";

            var DecrementTag = ZeroOrOne(Identifier).AndSkip(TagEnd)
                        .Then<Statement>(x => new DecrementStatement(x))
                        .ElseError("Invalid 'decrement' tag")
                        ;
            DecrementTag.Name = "DecrementTag";

            var IncrementTag = ZeroOrOne(Identifier).AndSkip(TagEnd)
                        .Then<Statement>(x => new IncrementStatement(x))
                        .ElseError("Invalid 'increment' tag")
                        ;
            IncrementTag.Name = "IncrementTag";

            var IncludeAssignStatement = Identifier.AndSkip(Colon).And(Primary).Then(static x => new AssignStatement(x.Item1, x.Item2));
            IncludeAssignStatement.Name = "IncludeAssignStatement";

            var IncludeTag = OneOf(
                        Primary.AndSkip(Terms.Text("with")).And(Primary).And(ZeroOrOne(Terms.Text("as").SkipAnd(Identifier))).Then(x => new IncludeStatement(this, x.Item1, with: x.Item2, alias: x.Item3)),
                        Primary.AndSkip(Terms.Text("for")).And(Primary).And(ZeroOrOne(Terms.Text("as").SkipAnd(Identifier))).Then(x => new IncludeStatement(this, x.Item1, @for: x.Item2, alias: x.Item3)),
                        Primary
                            .And(ZeroOrOne(ZeroOrOne(Comma).SkipAnd(Separated(Comma, IncludeAssignStatement))))
                            .AndSkip(ZeroOrOne(Comma))
                            .Then(x => new IncludeStatement(this, x.Item1, null, null, null, x.Item2 ?? []))
                        ).AndSkip(TagEnd)
                        .Then<Statement>(x => x)
                        .ElseError("Invalid 'include' tag")
                        ;
            IncludeTag.Name = "IncludeTag";

            var FromTag = OneOf(
                        Primary.AndSkip(Terms.Text("import")).And(Separated(Comma, Identifier)).Then(x => new FromStatement(this, x.Item1, x.Item2)),
                        Primary.Then(x => new FromStatement(this, x))
                        ).AndSkip(TagEnd)
                        .Then<Statement>(x => x)
                        .ElseError("Invalid 'from' tag")
                        ;
            FromTag.Name = "FromTag";

            var RenderTag = OneOf(
                        String.AndSkip(Terms.Text("with")).And(Primary).And(ZeroOrOne(Terms.Text("as").SkipAnd(Identifier))).And(ZeroOrOne(ZeroOrOne(Comma).SkipAnd(Separated(Comma, Identifier.AndSkip(Colon).And(Primary).Then(static x => new AssignStatement(x.Item1, x.Item2)))))).Then(x => new RenderStatement(this, x.Item1.ToString(), with: x.Item2, alias: x.Item3, assignStatements: x.Item4 ?? [])),
                        String.AndSkip(Terms.Text("for")).And(Primary).And(ZeroOrOne(Terms.Text("as").SkipAnd(Identifier))).And(ZeroOrOne(ZeroOrOne(Comma).SkipAnd(Separated(Comma, Identifier.AndSkip(Colon).And(Primary).Then(static x => new AssignStatement(x.Item1, x.Item2)))))).Then(x => new RenderStatement(this, x.Item1.ToString(), @for: x.Item2, alias: x.Item3, assignStatements: x.Item4 ?? [])),
                        String.And(ZeroOrOne(ZeroOrOne(Comma).SkipAnd(Separated(Comma, Identifier.AndSkip(Colon).And(Primary).Then(static x => new AssignStatement(x.Item1, x.Item2)))))).Then(x => new RenderStatement(this, x.Item1.ToString(), null, null, null, x.Item2 ?? []))
                        ).ElseError(ErrorMessages.ExpectedStringRender).AndSkip(TagEnd)
                        .Then<Statement>(x => x)
                        .ElseError("Invalid 'render' tag")
                        ;
            RenderTag.Name = "RenderTag";

            var RawTag = TagEnd.SkipAnd(AnyCharBefore(CreateTag("endraw"), canBeEmpty: true, consumeDelimiter: true, failOnEof: true).Then<Statement>(x => new RawStatement(x))).ElseError("Not end tag found for {% raw %}");
            RawTag.Name = "RawTag";

            var AssignTag = Identifier.Then(x => x).ElseError(ErrorMessages.IdentifierAfterAssign).AndSkip(Equal.ElseError(ErrorMessages.EqualAfterAssignIdentifier)).And(FilterExpression).AndSkip(TagEnd.ElseError(ErrorMessages.ExpectedTagEnd)).Then<Statement>(x => new AssignStatement(x.Item1, x.Item2));
            AssignTag.Name = "AssignTag";

            // Lenient else tag: matches {% else %} or {% else anything %} (extra content is ignored)
            var ElseTagLenient = TagStart.SkipAnd(Terms.Text("else")).SkipAnd(AnyCharBefore(TagEnd, canBeEmpty: true)).AndSkip(TagEnd);
            
            // Elsif tag for capturing stray elsif blocks (after else, they should be ignored)
            var ElsifTagCapture = TagStart.SkipAnd(Terms.Text("elsif")).SkipAnd(AnyCharBefore(TagEnd, canBeEmpty: true)).AndSkip(TagEnd);

            // Content that can follow an else block - includes regular content plus any stray else/elsif blocks
            // This ensures that {% else %}content{% elsif %}more{% else %}even_more is all captured, 
            // with only the first else block's content being used
            var ElseContent = AnyTagsList;
            
            // Parser for else blocks that also consumes any trailing elsif/else blocks
            // Multiple else blocks: only first is used. Elsif after else: ignored.
            var ElseBlocksWithTrailing = ZeroOrMany(
                OneOf(
                    ElseTagLenient.SkipAnd(AnyTagsList).Then(x => (IReadOnlyList<Statement>)x),
                    ElsifTagCapture.SkipAnd(AnyTagsList).Then(x => (IReadOnlyList<Statement>)null) // elsif after else is ignored
                )).Then(x => {
                    // Find first non-null (first else block content)
                    return new ElseStatement(x.FirstOrDefault(e => e != null)) ?? null;
                });

            var IfTag = LogicalExpression
                        .AndSkip(TagEnd)
                        .And(AnyTagsList)
                        .And(ZeroOrMany(
                            TagStart.SkipAnd(Terms.Text("elsif")).SkipAnd(LogicalExpression).AndSkip(TagEnd).And(AnyTagsList))
                            .Then(x => x.Select(e => new ElseIfStatement(e.Item1, e.Item2)).ToList()))
                        .And(ElseBlocksWithTrailing)
                        .AndSkip(CreateTag("endif").ElseError($"'{{% endif %}}' was expected"))
                        .Then<Statement>(x => new IfStatement(x.Item1, x.Item2, x.Item4, x.Item3))
                        .ElseError("Invalid 'if' tag");
            IfTag.Name = "IfTag";

            var UnlessTag = LogicalExpression
                        .AndSkip(TagEnd)
                        .And(AnyTagsList)
                        .And(ZeroOrMany(
                            TagStart.SkipAnd(Terms.Text("elsif")).SkipAnd(LogicalExpression).AndSkip(TagEnd).And(AnyTagsList))
                            .Then(x => x.Select(e => new ElseIfStatement(e.Item1, e.Item2)).ToList()))
                        .And(ElseBlocksWithTrailing)
                        .AndSkip(CreateTag("endunless").ElseError($"'{{% endunless %}}' was expected"))
                        .Then<Statement>(x => new UnlessStatement(x.Item1, x.Item2, x.Item4, x.Item3))
                        .ElseError("Invalid 'unless' tag");
            UnlessTag.Name = "UnlessTag";

            // Parser for optional comment tags only (used between case blocks)
            var OptionalComment = TagStart.SkipAnd(Terms.Text("comment")).SkipAnd(TagEnd)
                .SkipAnd(AnyCharBefore(CreateTag("endcomment"), canBeEmpty: true))
                .AndSkip(CreateTag("endcomment"))
                .Then<Statement>(x => new CommentStatement(x));
            
            var OptionalComments = ZeroOrMany(OneOf<Statement>(OptionalComment, Text));
            OptionalComments.Name = "OptionalComments";

            // Parse a single when or else block
            var WhenBlock = TagStart.AndSkip(Terms.Text("when")).And(CaseValueList.ElseError("Invalid 'when' tag")).AndSkip(TagEnd).And(AnyTagsList)
                .Then<CaseBlock>(x => new Ast.WhenBlock(x.Item2, x.Item3));
            
            var ElseBlock = CreateTag("else").SkipAnd(AnyTagsList)
                .Then<CaseBlock>(x => new Ast.ElseBlock(x));

            var CaseTag = Primary
                       .AndSkip(TagEnd)
                       .AndSkip(OptionalComments)
                       .And(ZeroOrMany(OneOf(WhenBlock, ElseBlock).AndSkip(OptionalComments)))
                       .AndSkip(CreateTag("endcase").ElseError($"'{{% endcase %}}' was expected"))
                       .Then<Statement>(x => new CaseStatement(x.Item1, x.Item2))
                       .ElseError("Invalid 'case' tag");
            CaseTag.Name = "CaseTag";

            var ForTag = Identifier
                            .AndSkip(Terms.Text("in"))
                            .And(Primary)
                            .And(ZeroOrMany(
                                ZeroOrOne(Comma)
                                .SkipAnd(OneOf( // Use * since each can appear in any order. Validation is done once it's parsed
                                    Terms.Text("reversed").Then(x => new ForModifier { IsReversed = true }),
                                    Terms.Text("limit").SkipAnd(Colon).SkipAnd(Primary).Then(x => new ForModifier { IsLimit = true, Value = x }),
                                    Terms.Text("offset").SkipAnd(Colon).SkipAnd(Primary).Then(x => new ForModifier { IsOffset = true, Value = x })
                                ))))
                            .AndSkip(ZeroOrOne(Comma))
                            .AndSkip(TagEnd)
                            .And(AnyTagsList)
                            .And(ZeroOrOne(
                                CreateTag("else").SkipAnd(AnyTagsList))
                                .Then(x => x != null ? new ElseStatement(x) : null))
                            .AndSkip(CreateTag("endfor").ElseError($"'{{% endfor %}}' was expected"))
                            .Then<Statement>(x =>
                            {
                                var identifier = x.Item1;
                                var source = x.Item2;
                                var statements = x.Item4;
                                var elseStatement = x.Item5;
                                var (limitResult, offsetResult, reversed) = ReadForStatementConfiguration(x.Item3);
                                return new ForStatement(statements, identifier, source, limitResult, offsetResult, reversed, elseStatement);
                            })
                        .ElseError("Invalid 'for' tag");
            ForTag.Name = "ForTag";

            var LiquidTag = Literals.WhiteSpace(true) // {% liquid %} can start with new lines
                .Then((context, x) => { ((FluidParseContext)context).InsideLiquidTag = true; return x; })
                .SkipAnd(OneOrMany(OneOf(
                    Terms.Char('#').Then(x => "#"),
                    Identifier
                ).Switch((context, previous) =>
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
            LiquidTag.Name = "LiquidTag";

            var EchoTag = FilterExpression.AndSkip(TagEnd).Then<Statement>(x => new OutputStatement(x));
            EchoTag.Name = "EchoTag";

            RegisteredTags["break"] = BreakTag;
            RegisteredTags["continue"] = ContinueTag;
            RegisteredTags["comment"] = CommentTag;
            RegisteredTags["#"] = InlineCommentTag;
            RegisteredTags["doc"] = DocTag;
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
                RegisteredTags["from"] = FromTag;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static (Expression limitResult, Expression offsetResult, bool reversed) ReadForStatementConfiguration(IReadOnlyList<ForModifier> modifiers)
            {
                if (modifiers.Count == 0)
                {
                    return (null, null, false);
                }

                // take slower route when needed
                static (Expression limitResult, Expression offsetResult, bool reversed) ReadFromList(IReadOnlyList<ForModifier> modifiers)
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

            var AnyTags = TagStart.SkipAnd(OneOf(
                Terms.Char('#').Then(x => "#"),
                Identifier.ElseError(ErrorMessages.IdentifierAfterTagStart)
            ).Switch((context, previous) =>
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

            var KnownTags = TagStart.SkipAnd(OneOf(
                Terms.Char('#').Then(x => "#"),
                Identifier.ElseError(ErrorMessages.IdentifierAfterTagStart)
            ).Switch((context, previous) =>
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

        public Parser<string> CreateTag(string tagName) => TagStart.SkipAnd(Terms.Text(tagName)).AndSkip(TagEnd);

        public void RegisterIdentifierTag(string tagName, Func<string, IFluidOutput, TextEncoder, TemplateContext, ValueTask<Completion>> render)
        {
            RegisterParserTag(tagName, Identifier.ElseError($"An identifier was expected after the '{tagName}' tag"), render);
        }

        public void RegisterIdentifierBlock(string tagName, Func<string, IReadOnlyList<Statement>, IFluidOutput, TextEncoder, TemplateContext, ValueTask<Completion>> render)
        {
            RegisterParserBlock(tagName, Identifier.ElseError($"An identifier was expected after the '{tagName}' tag"), render);
        }

        public void RegisterExpressionBlock(string tagName, Func<Expression, IReadOnlyList<Statement>, IFluidOutput, TextEncoder, TemplateContext, ValueTask<Completion>> render)
        {
            RegisterParserBlock(tagName, FilterExpression, render);
        }

        public void RegisterExpressionTag(string tagName, Func<Expression, IFluidOutput, TextEncoder, TemplateContext, ValueTask<Completion>> render)
        {
            RegisterParserTag(tagName, FilterExpression, render);
        }

        public void RegisterParserBlock<T>(string tagName, Parser<T> parser, Func<T, IReadOnlyList<Statement>, IFluidOutput, TextEncoder, TemplateContext, ValueTask<Completion>> render)
        {
            RegisteredTags[tagName] = parser.AndSkip(TagEnd).And(AnyTagsList).AndSkip(CreateTag("end" + tagName).ElseError($"'{{% end{tagName} %}}' was expected"))
                .Then<Statement>(x => new ParserBlockStatement<T>(tagName, x.Item1, x.Item2, render))
                .ElseError($"Invalid {tagName} tag")
                ;
        }

        public void RegisterParserTag<T>(string tagName, Parser<T> parser, Func<T, IFluidOutput, TextEncoder, TemplateContext, ValueTask<Completion>> render)
        {
            RegisteredTags[tagName] = parser.AndSkip(TagEnd).Then<Statement>(x => new ParserTagStatement<T>(tagName, x, render));
            RegisteredTags[tagName].Name = tagName;
        }

        public void RegisterEmptyTag(string tagName, Func<IFluidOutput, TextEncoder, TemplateContext, ValueTask<Completion>> render)
        {
            RegisteredTags[tagName] = TagEnd.Then<Statement>(x => new EmptyTagStatement(tagName, render)).ElseError($"Unexpected arguments in {tagName} tag");
            RegisteredTags[tagName].Name = tagName;
        }

        public void RegisterEmptyBlock(string tagName, Func<IReadOnlyList<Statement>, IFluidOutput, TextEncoder, TemplateContext, ValueTask<Completion>> render)
        {
            RegisteredTags[tagName] = TagEnd.SkipAnd(AnyTagsList).AndSkip(CreateTag("end" + tagName).ElseError($"'{{% end{tagName} %}}' was expected"))
                .Then<Statement>(x => new EmptyBlockStatement(tagName, x, render))
                .ElseError($"Invalid '{tagName}' tag")
                ;
            RegisteredTags[tagName].Name = tagName;
        }

        /// <summary>
        /// Compiles all expressions.
        /// </summary>
        public virtual FluidParser Compile()
        {
            foreach (var entry in RegisteredTags)
            {
                RegisteredTags[entry.Key] = entry.Value.Compile();
            }

            Grammar = Grammar.Compile();

            return this;
        }
    }
}
