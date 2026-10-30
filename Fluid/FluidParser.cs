#pragma warning disable FLUID001

using Fluid.Ast;
using Fluid.Ast.BinaryExpressions;
using Fluid.Parser;
using Fluid.Values;
using Parlot;
using Parlot.Fluent;
using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;

namespace Fluid;

public class FluidParser
{
    private readonly bool _trackStatementLocations;
    private readonly Parser<TextSpan> _tagName;
    public Parser<IReadOnlyList<Statement>> Grammar;
    public Dictionary<string, Parser<Statement>> RegisteredTags { get; } = new();
    public Dictionary<string, Func<Expression, Expression, Expression>> RegisteredOperators { get; } = new();

    private readonly Deferred<Statement> _anyRegisteredTag = Deferred<Statement>();
    private readonly Deferred<Statement> _knownRegisteredTag = Deferred<Statement>();
    private readonly Deferred<Statement> _liquidRegisteredTag = Deferred<Statement>();
    private readonly Lock _registeredTagLock = new();
    private KeyValuePair<string, Parser<Statement>>[] _registeredTagSnapshot = [];

    protected static readonly Parser<char> LBrace = Terms.Char('{');
    protected static readonly Parser<char> RBrace = Terms.Char('}');
    protected static readonly Parser<char> LParen = Terms.Char('(');
    protected static readonly Parser<char> RParen = Terms.Char(')');
    protected static readonly Parser<char> LBracket = Literals.Char('[');
    protected static readonly Parser<char> RBracket = Terms.Char(']');
    protected static readonly Parser<char> Equal = Terms.Char('=');
    protected static readonly Parser<char> Colon = Terms.Char(':');
    protected static readonly Parser<char> Comma = Terms.Char(',');
    protected static readonly Parser<char> Dot = Terms.Char('.'); // Terms.Char skips leading whitespace to allow `foo .bar`
    protected static readonly Parser<char> Pipe = Terms.Char('|');

    // Like '\w' in Shopify's regular expressions. A Liquid word can't be followed by one of these chars: "orb" is not "or".
    // '-' is not one of them, so a trim marker can follow a word ("{%else-%}"), and "else-x" is "else" like in Shopify.
    // Parlot's Keyword can't be used yet: only ASCII letters continue a keyword, so "or" would match "or1" or "or_b".
    private const string WordChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_";
    private static readonly Parser<TextSpan> WordChar = Literals.AnyOf(WordChars, maxSize: 1);

    protected readonly Parser<TextSpan> String;
    protected static readonly Parser<string> DoubleEquals = Terms.Text("==");
    protected static readonly Parser<string> NotEquals = Terms.Text("!=");
    protected static readonly Parser<string> Different = Terms.Text("<>");
    protected static readonly Parser<string> Greater = Terms.Text(">");
    protected static readonly Parser<string> Lower = Terms.Text("<");
    protected static readonly Parser<string> GreaterOr = Terms.Text(">=");
    protected static readonly Parser<string> LowerOr = Terms.Text("<=");
    protected static readonly Parser<string> Contains = Word("contains");
    protected static readonly Parser<string> StartsWith = Word("startswith");
    protected static readonly Parser<string> EndsWith = Word("endswith");
    protected static readonly Parser<string> BinaryOr = Word("or");
    protected static readonly Parser<string> BinaryAnd = Word("and");

    protected readonly Parser<string> Identifier;
    protected readonly Parser<string> VariableSignature;

    protected readonly Parser<IReadOnlyList<FilterArgument>> ArgumentsList;
    protected readonly Parser<IReadOnlyList<FunctionCallArgument>> FunctionCallArgumentsList;
    protected readonly Parser<Expression> LogicalExpression;
    protected readonly Parser<Expression> CombinatoryExpression; // and | or
    protected readonly Deferred<Expression> Primary = Deferred<Expression>();
    protected readonly Deferred<Expression> FilterExpression = Deferred<Expression>();
    protected readonly Deferred<IReadOnlyList<Statement>> KnownTagsList = Deferred<IReadOnlyList<Statement>>();
    protected readonly Deferred<IReadOnlyList<Statement>> AnyTagsList = Deferred<IReadOnlyList<Statement>>();

    protected static readonly Parser<TagResult> InlineOutputStart = TagParsers.OutputTagStart();
    protected static readonly Parser<TagResult> InlineOutputEnd = TagParsers.OutputTagEnd();
    protected static readonly Parser<TagResult> InlineTagStart = TagParsers.TagStart();
    protected static readonly Parser<TagResult> InlineTagEnd = TagParsers.TagEnd();

    protected static readonly Parser<TagResult> NoInlineOutputStart = NonInlineLiquidTagParsers.OutputTagStart();
    protected static readonly Parser<TagResult> NoInlineOutputEnd = SkipWhiteSpace(NonInlineLiquidTagParsers.OutputTagEnd());
    protected static readonly Parser<TagResult> NoInlineTagStart = NonInlineLiquidTagParsers.TagStart();
    protected static readonly Parser<TagResult> NoInlineTagEnd = SkipWhiteSpace(NonInlineLiquidTagParsers.TagEnd());

    protected readonly Parser<TagResult> OutputStart = InlineOutputStart;
    protected readonly Parser<TagResult> OutputEnd = InlineOutputEnd;
    protected readonly Parser<TagResult> TagStart = InlineTagStart;
    protected readonly Parser<TagResult> TagEnd = InlineTagEnd;

    // Like in Shopify, the tags without arguments, like end tags, ignore their markup: {% endif anything %}
    private readonly Parser<TagResult> _tagEndIgnoringMarkup;

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
        _trackStatementLocations = parserOptions.TrackStatementLocations;
        if (!parserOptions.AllowLiquidTag)
        {
            OutputStart = NoInlineOutputStart;
            OutputEnd = NoInlineOutputEnd;
            TagStart = NoInlineTagStart;
            TagEnd = NoInlineTagEnd;
        }

        _tagEndIgnoringMarkup = TagEnd.Or(AnyCharBefore(TagEnd).SkipAnd(TagEnd));

        Identifier = SkipWhiteSpace(IdentifierParser.Create(parserOptions.AllowTrailingQuestionMark)).Then(x => x.ToString());
        // Like in Shopify, a tag name is made of word chars: "{% comment-x %}" is a comment tag.
        _tagName = SkipWhiteSpace(OneOf(Literals.Char('#').Then(new TextSpan("#")), Literals.AnyOf(WordChars)));
        VariableSignature = SkipWhiteSpace(VariableSignatureParser.Create()).Then(x => x.ToString());

        // Like Liquid, a backslash in a string literal is a plain character unless escape sequences are enabled.
        String = parserOptions.AllowStringEscapeSequences
            ? Terms.String(StringLiteralQuotes.SingleOrDouble)
            : SkipWhiteSpace(new StringLiteralParser());
        String.Name = "String";

        var Integer = Terms.Integer().Then<Expression>(x => new LiteralExpression(NumberValue.Create(x)));
        Integer.Name = "Integer";

        var Number = Terms.Decimal().Then<Expression>(x => new LiteralExpression(NumberValue.Create(x)));
        Number.Name = "Number";

        // Member expressions
        // Like Liquid, whitespace is allowed before an indexer: `products [0]` is `products[0]`.
        var Indexer = Between(Terms.Char('['), Primary, RBracket).Then<MemberSegment>(x => new IndexerSegment(x));
        Indexer.Name = "Indexer";

        // ([name =] value,)+
        FunctionCallArgumentsList = Separated(Comma,
                        OneOf(
                            Identifier.AndSkip(Equal).And(Primary).Then(static x => new FunctionCallArgument(x.Item1, x.Item2)),
                            Primary.Then(static x => new FunctionCallArgument(null, x))
                        )).Else(default(IReadOnlyList<FunctionCallArgument>));
        FunctionCallArgumentsList.Name = "FunctionArgumentsList";

        // (name [= value],)+
        var FunctionDefinitionArgumentsList = Separated(Comma,
                        Identifier.And(Equal.SkipAnd(Primary).Else(default(Expression))).Then(static x => new FunctionCallArgument(x.Item1, x.Item2)))
                        .Else(default(IReadOnlyList<FunctionCallArgument>));
        FunctionDefinitionArgumentsList.Name = "FunctionDefinitionArgumentsList";

        var Call = parserOptions.AllowFunctions
            ? LParen.SkipAnd(FunctionCallArgumentsList).AndSkip(RParen).Then<MemberSegment>(x => new FunctionCallSegment(x))
            : LParen.Error<MemberSegment>(ErrorMessages.FunctionsNotAllowed)
            ;
        Call.Name = "Call";

        // An Identifier followed by a list of MemberSegments (dot accessor, indexer or arguments list)
        // Note: Numeric index with dot notation (e.g., products.0) is not allowed per Shopify Liquid standard.
        // Use bracket notation instead: products[0]
        var Member = Identifier.Then<MemberSegment>(x => new IdentifierSegment(x)).And(
            ZeroOrMany(
                Dot.SkipAnd(Identifier.Then<MemberSegment>(x => new IdentifierSegment(x)))
                .Or(Indexer)
                .Or(Call)))
            .Then(x => new MemberExpression([x.Item1, .. x.Item2]));
        Member.Name = "Member";

        // Like Liquid, literal range bounds are truncated: (2.7..4) is (2..4).
        var RangeBound = Terms.Decimal().Then<Expression>(x => new LiteralExpression(NumberValue.Create(decimal.Truncate(x))));
        RangeBound.Name = "RangeBound";

        // Like Liquid, a bound can also be a string, converted to an integer when the range is evaluated.
        var StringRangeBound = String.Then<Expression>(x => new LiteralExpression(StringValue.Create(x)));
        StringRangeBound.Name = "StringRangeBound";

        var Range = LParen
            .SkipAnd(OneOf(
                Member.AndSkip(Terms.Text("..")),
                Integer.AndSkip(Terms.Text("..")),
                RangeBound.AndSkip(Terms.Text("..")),
                StringRangeBound.AndSkip(Terms.Text(".."))
            ))
            .And(OneOf(RangeBound, StringRangeBound, Member.Then<Expression>(x => x)))
            .AndSkip(RParen)
            .Then<Expression>(x => new RangeExpression(x.Item1, x.Item2));
        Range.Name = "Range";

        var Group = parserOptions.AllowParentheses
            ? LParen.SkipAnd(FilterExpression).AndSkip(RParen)
            : LParen.SkipAnd(FilterExpression).AndSkip(RParen).Error<Expression>(ErrorMessages.ParenthesesNotAllowed)
            ;
        Group.Name = "Group";

        // Bracketed access: [expr] followed by optional member accessors (e.g., [123] or ["foo"].bar or ['foo']['bar'])
        // This allows accessing scope by computed key, compatible with Shopify Liquid's variable signature behavior
        // Uses Terms.Char to skip leading whitespace (unlike LBracket which is Literals.Char)
        // Also allows chained brackets like ['foo']['bar'] with whitespace between them
        // Creates a MemberExpression with an IndexerSegment as the first segment
        var ChainedIndexer = Terms.Char('[').SkipAnd(Primary).AndSkip(RBracket).Then<MemberSegment>(x => new IndexerSegment(x));
        ChainedIndexer.Name = "ChainedIndexer";

        var BracketedAccess = Terms.Char('[').SkipAnd(Primary).AndSkip(RBracket).Then<MemberSegment>(x => new IndexerSegment(x))
            .And(ZeroOrMany(
                Dot.SkipAnd(Identifier.Then<MemberSegment>(x => new IdentifierSegment(x)))
                .Or(ChainedIndexer)
                .Or(Indexer)
                .Or(Call)))
            .Then<Expression>(x => new MemberExpression([x.Item1, .. x.Item2]));
        BracketedAccess.Name = "BracketedAccess";

        // primary => NUMBER | STRING | property
        Primary.Parser =
            String.Then<Expression>(x => new LiteralExpression(StringValue.Create(x)))
            .Or(BracketedAccess)
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

        var CaseValueList = Separated(BinaryOr.Or(Terms.Text(",")), Primary);
        CaseValueList.Name = "CaseValueList";

        // Seek anything that looks like a binary operator (==, !=, <, >, <=, >=, contains, startswith, endswith) then validates it with the registered operators
        // An identifier reads all the word chars, so "containsx" is not "contains", but like in Shopify "contains" can be followed by a string: x contains"y"
        CombinatoryExpression = Primary.And(OneOf(Terms.AnyOf("=!<>".AsSpan(), maxSize: 2), Terms.Identifier()).Then(x => x.ToString())
            .When((ctx, s) => RegisteredOperators.ContainsKey(s)).And(Primary).Else(default((string, Expression))))
            .Then(x =>
             {
                 if (x.Item2.Item1 == null)
                 {
                     return x.Item1;
                 }

                 return RegisteredOperators[x.Item2.Item1](x.Item1, x.Item2.Item2);
             }).Named("CombinatoryExpression");

        LogicalExpression = CombinatoryExpression.And(ZeroOrMany(OneOf(BinaryOr, BinaryAnd).And(CombinatoryExpression)))
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
        FilterExpression.Parser = LogicalExpression
            .And(ZeroOrMany(
                Pipe
                .SkipAnd(Identifier.ElseError(ErrorMessages.IdentifierAfterPipe))
                .And(Colon.SkipAnd(ArgumentsList).Else(default(IReadOnlyList<FilterArgument>)))))
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

        Parser<Statement> Output = OutputStart.SkipAnd(
            // Like Liquid, an output without an expression renders nothing.
            OutputEnd.Then<Statement>(static x => new OutputStatement(EmptyKeyword))
            .Or(FilterExpression.ElseError(ErrorMessages.LogicalExpressionStartsFilter).And(OutputEnd.ElseError(ErrorMessages.ExpectedOutputEnd))
                .Then<Statement>(static x => new OutputStatement(x.Item1)))
            );
        Output.Name = "Output";
        Output = TrackStatement(Output);

        // A text ends at the next '{{' or '{%', which the seekable delimiters let it search for. Inside a {% liquid %}
        // tag, where tags don't start with '{%', there is no text.
        var TextContent = AnyCharBefore(RawOutputStart.Or(RawTagStart));

        if (parserOptions.AllowLiquidTag)
        {
            TextContent = If<FluidParseContext, TextSpan>(static context => context.LiquidTagDepth == 0, TextContent);
        }

        var Text = TextContent.Then<Statement>(static x => CreateTextSpanStatement(x));
        Text.Name = "Text";
        Text = TrackStatement(Text);

        var BreakTag = _tagEndIgnoringMarkup.Then<Statement>(x => new BreakStatement()).ElseError("Invalid 'break' tag");
        BreakTag.Name = "BreakTag";

        var ContinueTag = _tagEndIgnoringMarkup.Then<Statement>(x => new ContinueStatement()).ElseError("Invalid 'continue' tag");
        ContinueTag.Name = "ContinueTag";

        // Like Shopify, the body of a comment is read as tags, outputs and text, the same way as a template.
        // The tags don't need to be valid, but nested comment and raw blocks must be balanced, and an endcomment
        // inside an incomplete tag or output (e.g. '{{ {% endcomment %}') doesn't close the comment.
        var TagClose = Literals.Text("%}");

        // A tag named 'name', from '{%' to the first '%}', e.g. '{%- name anything -%}'.
        Parser<TextSpan> NamedTag(string name) => Capture(Literals.Text("{%")
            .SkipAnd(ZeroOrOne(Literals.Char('-')))
            .SkipAnd(Word(name))
            .SkipAnd(AnyCharBefore(TagClose, canBeEmpty: true, failOnEof: true))
            .SkipAnd(TagClose));

        var EndRaw = NamedTag("endraw");
        var RawInComment = Capture(NamedTag("raw")
            .SkipAnd(AnyCharBefore(EndRaw, canBeEmpty: true, failOnEof: true).AndSkip(EndRaw).ElseError($"'{{% endraw %}}' was expected")));

        // Any other tag. An unclosed '{%' is read alone.
        var TagInComment = Capture(Literals.Text("{%")
            .SkipAnd(ZeroOrOne(AnyCharBefore(TagClose, canBeEmpty: true, failOnEof: true).SkipAnd(TagClose))));

        // An output ends at the first '}' or '}}', unless a '{%' comes first: then it extends to the next '%}'.
        var OutputInComment = Capture(Literals.Text("{{")
            .SkipAnd(AnyCharBefore(OneOf(Literals.Text("}"), Literals.Text("{%")), canBeEmpty: true))
            .SkipAnd(ZeroOrOne(OneOf(
                Capture(Literals.Text("}}")),
                Capture(Literals.Text("}")),
                TagInComment))));

        var TextInComment = AnyCharBefore(OneOf(Literals.Text("{{"), Literals.Text("{%")));

        var EndComment = NamedTag("endcomment");

        var ShopifyCommentBody = Recursive<TextSpan>(body => Capture(ZeroOrMany(OneOf(
            Capture(NamedTag("comment").SkipAnd(body).AndSkip(EndComment.ElseError($"'{{% endcomment %}}' was expected"))),
            RawInComment,
            Not(EndComment).SkipAnd(TagInComment),
            OutputInComment,
            TextInComment))));

        // Inside a {% liquid %} tag, the comment ends at the first endcomment line.
        var CommentBody = If<FluidParseContext, TextSpan>(
            static context => context.LiquidTagDepth > 0,
            AnyCharBefore(CreateTag("endcomment"), canBeEmpty: true),
            ShopifyCommentBody);

        var CommentTag = _tagEndIgnoringMarkup
                    .SkipAnd(CommentBody)
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

        var CaptureTag = VariableSignature.ElseError(string.Format(ErrorMessages.IdentifierAfterTag, "capture"))
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

        var CycleTag = Primary.AndSkip(Colon).Else(default(Expression))
                    .And(Separated(Comma, Primary))
                    .AndSkip(TagEnd)
                    .Then<Statement>(x => new CycleStatement(x.Item1, x.Item2))
                    .ElseError("Invalid 'cycle' tag")
                    ;
        CycleTag.Name = "CycleTag";

        var IfChangedTag = _tagEndIgnoringMarkup
                    .And(AnyTagsList)
                    .AndSkip(CreateTag("endifchanged").ElseError($"'{{% endifchanged %}}' was expected"))
                    .Then<Statement>(x => new IfChangedStatement(x.Item2))
                    .ElseError("Invalid 'ifchanged' tag")
                    ;
        IfChangedTag.Name = "IfChangedTag";

        var DecrementTag = VariableSignature.Else(default(string)).AndSkip(TagEnd)
                    .Then<Statement>(x => new DecrementStatement(x))
                    .ElseError("Invalid 'decrement' tag")
                    ;
        DecrementTag.Name = "DecrementTag";

        var IncrementTag = VariableSignature.Else(default(string)).AndSkip(TagEnd)
                    .Then<Statement>(x => new IncrementStatement(x))
                    .ElseError("Invalid 'increment' tag")
                    ;
        IncrementTag.Name = "IncrementTag";

        var IncludeAssignStatement = Identifier.AndSkip(Colon).And(Primary).Then(static x => new AssignStatement(x.Item1, x.Item2));
        IncludeAssignStatement.Name = "IncludeAssignStatement";
        IncludeAssignStatement = TrackStatement(IncludeAssignStatement, skipLeadingWhitespace: true);

        var IncludeTag = OneOf(
                    Primary.AndSkip(Word("with")).And(Primary).And(Word("as").SkipAnd(Identifier).Optional()).And(Comma.Optional().SkipAnd(Separated(Comma, IncludeAssignStatement)).Optional()).Then(x => new IncludeStatement(this, x.Item1, with: x.Item2, alias: x.Item3.HasValue ? x.Item3.Value : null, assignStatements: x.Item4.HasValue ? x.Item4.Value : [])),
                    Primary.AndSkip(Word("for")).And(Primary).And(Word("as").SkipAnd(Identifier).Optional()).And(Comma.Optional().SkipAnd(Separated(Comma, IncludeAssignStatement)).Optional()).Then(x => new IncludeStatement(this, x.Item1, @for: x.Item2, alias: x.Item3.HasValue ? x.Item3.Value : null, assignStatements: x.Item4.HasValue ? x.Item4.Value : [])),
                    Primary
                        .And(Comma.Optional().SkipAnd(Separated(Comma, IncludeAssignStatement)).Optional())
                        .AndSkip(Comma.Optional())
                        .Then(x => new IncludeStatement(this, x.Item1, null, null, null, x.Item2.HasValue ? x.Item2.Value : []))
                    ).AndSkip(TagEnd)
                    .Then<Statement>(x => x)
                    .ElseError("Invalid 'include' tag")
                    ;
        IncludeTag.Name = "IncludeTag";

        var FromTag = OneOf(
                    Primary.AndSkip(Word("import")).And(Separated(Comma, Identifier)).Then(x => new FromStatement(this, x.Item1, x.Item2)),
                    Primary.Then(x => new FromStatement(this, x))
                    ).AndSkip(TagEnd)
                    .Then<Statement>(x => x)
                    .ElseError("Invalid 'from' tag")
                    ;
        FromTag.Name = "FromTag";

        var RenderTag = OneOf(
                    String.AndSkip(Word("with")).And(Primary).And(Word("as").SkipAnd(Identifier).Optional()).And(Comma.Optional().SkipAnd(Separated(Comma, IncludeAssignStatement)).Optional()).Then(x => new RenderStatement(this, x.Item1.ToString(), with: x.Item2, alias: x.Item3.HasValue ? x.Item3.Value : null, assignStatements: x.Item4.HasValue ? x.Item4.Value : [])),
                    String.AndSkip(Word("for")).And(Primary).And(Word("as").SkipAnd(Identifier).Optional()).And(Comma.Optional().SkipAnd(Separated(Comma, IncludeAssignStatement)).Optional()).Then(x => new RenderStatement(this, x.Item1.ToString(), @for: x.Item2, alias: x.Item3.HasValue ? x.Item3.Value : null, assignStatements: x.Item4.HasValue ? x.Item4.Value : [])),
                    String.And(Comma.Optional().SkipAnd(Separated(Comma, IncludeAssignStatement)).Optional()).Then(x => new RenderStatement(this, x.Item1.ToString(), null, null, null, x.Item2.HasValue ? x.Item2.Value : []))
                    ).ElseError(ErrorMessages.ExpectedStringRender).AndSkip(TagEnd)
                    .Then<Statement>(x => x)
                    .ElseError("Invalid 'render' tag")
                    ;
        RenderTag.Name = "RenderTag";

        var RawTag = _tagEndIgnoringMarkup.SkipAnd(AnyCharBefore(CreateTag("endraw"), canBeEmpty: true, consumeDelimiter: true, failOnEof: true).Then<Statement>(x => new RawStatement(x))).ElseError("Not end tag found for {% raw %}");
        RawTag.Name = "RawTag";

        // Like Liquid, an assign without a value assigns nil.
        var AssignTag = VariableSignature.Then(x => x).ElseError(ErrorMessages.IdentifierAfterAssign).AndSkip(Equal.ElseError(ErrorMessages.EqualAfterAssignIdentifier)).And(FilterExpression.Or(Always<Expression>(NilKeyword).WhenFollowedBy(TagEnd)).ElseError(ErrorMessages.LogicalExpressionStartsFilter)).AndSkip(TagEnd.ElseError(ErrorMessages.ExpectedTagEnd)).Then<Statement>(x => new AssignStatement(x.Item1, x.Item2));
        AssignTag.Name = "AssignTag";

        // Lenient else tag: matches {% else %} or {% else anything %} (extra content is ignored)
        var ElseTagLenient = CreateTag("else");

        // Elsif tag for capturing stray elsif blocks (after else, they should be ignored)
        var ElsifTagCapture = CreateTag("elsif");

        // Content that can follow an else block - includes regular content plus any stray else/elsif blocks
        // This ensures that {% else %}content{% elsif %}more{% else %}even_more is all captured, 
        // with only the first else block's content being used
        var ElseContent = AnyTagsList;

        // Parser for else blocks that also consumes any trailing elsif/else blocks
        // Multiple else blocks: only first is used. Elsif after else: ignored.
        var ElseBlocksWithTrailing = ZeroOrMany(
            OneOf(
                TrackStatement(ElseTagLenient.SkipAnd(AnyTagsList).Then(x => new ElseStatement(x)), skipLeadingWhitespace: true),
                ElsifTagCapture.SkipAnd(AnyTagsList).Then(x => (ElseStatement)null) // elsif after else is ignored
            )).Then(x =>
            {
                // Find first non-null (first else block content)
                return x.FirstOrDefault(e => e != null) ?? new ElseStatement(null);
            });

        var ElseIfBranch = TrackStatement(
            TagStart.SkipAnd(Word("elsif")).SkipAnd(LogicalExpression).AndSkip(TagEnd).And(AnyTagsList)
                .Then(x => new ElseIfStatement(x.Item1, x.Item2)), skipLeadingWhitespace: true);

        var IfTag = LogicalExpression
                    .AndSkip(TagEnd)
                    .And(AnyTagsList)
                    .And(ZeroOrMany(ElseIfBranch))
                    .And(ElseBlocksWithTrailing)
                    .AndSkip(CreateTag("endif").ElseError($"'{{% endif %}}' was expected"))
                    .Then<Statement>(x => new IfStatement(x.Item1, x.Item2, x.Item4, x.Item3))
                    .ElseError("Invalid 'if' tag");
        IfTag.Name = "IfTag";

        var UnlessTag = LogicalExpression
                    .AndSkip(TagEnd)
                    .And(AnyTagsList)
                    .And(ZeroOrMany(ElseIfBranch))
                    .And(ElseBlocksWithTrailing)
                    .AndSkip(CreateTag("endunless").ElseError($"'{{% endunless %}}' was expected"))
                    .Then<Statement>(x => new UnlessStatement(x.Item1, x.Item2, x.Item4, x.Item3))
                    .ElseError("Invalid 'unless' tag");
        UnlessTag.Name = "UnlessTag";

        // Parser for optional comment tags only (used between case blocks)
        var OptionalComment = CreateTag("comment")
            .SkipAnd(CommentBody)
            .AndSkip(CreateTag("endcomment"))
            .Then<Statement>(x => new CommentStatement(x));

        var OptionalComments = ZeroOrMany(OneOf<Statement>(OptionalComment, Text));
        OptionalComments.Name = "OptionalComments";

        // Parse a single when or else block
        // After parsing the case value list, skip any unexpected content until %} (like 'and' which is not valid in when)
        // This matches Shopify Liquid's behavior where unexpected tokens are ignored
        var WhenBlock = TagStart.AndSkip(Word("when")).And(CaseValueList.ElseError("Invalid 'when' tag")).AndSkip(AnyCharBefore(TagEnd, canBeEmpty: true)).AndSkip(TagEnd).And(AnyTagsList)
            .Then<CaseBlock>(x => new WhenBlock(x.Item2, x.Item3));

        var ElseBlock = CreateTag("else").SkipAnd(AnyTagsList)
            .Then<CaseBlock>(x => new ElseBlock(x));

        var CaseTag = Primary
                   .AndSkip(TagEnd)
                   .AndSkip(OptionalComments)
                   .And(ZeroOrMany(OneOf(WhenBlock, ElseBlock).AndSkip(OptionalComments)))
                   .AndSkip(CreateTag("endcase").ElseError($"'{{% endcase %}}' was expected"))
                   .Then<Statement>(x => new CaseStatement(x.Item1, x.Item2))
                   .ElseError("Invalid 'case' tag");
        CaseTag.Name = "CaseTag";

        // Like in Shopify, "in" is followed by whitespace: "{% for i in(1..3) %}" is invalid.
        var In = Terms.Text("in").AndSkip(Literals.WhiteSpace(includeNewLines: true));

        // Like in Shopify, the "name: value" attributes of a loop are read anywhere after its collection, and anything else is ignored,
        // like "foo", "limitx: 2" and the commas in "{% for i in items foo, limitx: 2, limit: 3 %}".
        var IgnoredLoopMarkup = OneOf(
            Identifier.AndSkip(Colon).AndSkip(Primary),
            Literals.Pattern(static _ => true, maxSize: 1).Then(static _ => default(string)));

        // Reads the attributes of a loop and the end of its tag, which most often comes right after the collection.
        Parser<IReadOnlyList<T>> LoopAttributes<T>(Parser<T> attributes) =>
            TagEnd.Then<IReadOnlyList<T>>(static _ => []).Or(
                ZeroOrMany(Not(TagEnd).SkipAnd(attributes.Or(IgnoredLoopMarkup.Then(static _ => default(T))))).AndSkip(TagEnd));

        // Like in Shopify, "reversed" is only read right after the collection, even when it starts a longer word ("reversedx").
        var Reversed = Always(true).WhenFollowedBy(Terms.Text("reversed")).Else(false);

        var ForTag = Identifier
                        .AndSkip(In)
                        .And(Primary)
                        .And(Reversed)
                        .And(LoopAttributes(OneOf(
                            Word("limit").SkipAnd(Colon).SkipAnd(Primary).Then(x => new ForModifier { IsLimit = true, Value = x }),
                            Word("offset").SkipAnd(Colon).SkipAnd(Primary).Then(x => new ForModifier { IsOffset = true, Value = x })
                            )))
                        .And(AnyTagsList)
                        .And(TrackStatement(
                            CreateTag("else").SkipAnd(AnyTagsList).Then(x => new ElseStatement(x)),
                            skipLeadingWhitespace: true).Else(default(ElseStatement)))
                        .AndSkip(CreateTag("endfor").ElseError($"'{{% endfor %}}' was expected"))
                        .Then<Statement>(x =>
                        {
                            var identifier = x.Item1;
                            var source = x.Item2;
                            var reversed = x.Item3;
                            var statements = x.Item5;
                            var elseStatement = x.Item6;
                            var (limitResult, offsetResult) = ReadForStatementConfiguration(x.Item4);
                            return new ForStatement(statements, identifier, source, limitResult, offsetResult, reversed, elseStatement);
                        })
                    .ElseError("Invalid 'for' tag");
        ForTag.Name = "ForTag";

        // Like in Shopify, "reversed" is ignored by a tablerow tag.
        var TableRowTag = Identifier
                        .AndSkip(In)
                        .And(Member.Then<Expression>(static x => x).Or(Range))
                        .And(LoopAttributes(OneOf(
                            Word("cols").SkipAnd(Colon).SkipAnd(Primary).Then(x => new TableRowModifier { IsCols = true, Value = x }),
                            Word("limit").SkipAnd(Colon).SkipAnd(Primary).Then(x => new TableRowModifier { IsLimit = true, Value = x }),
                            Word("offset").SkipAnd(Colon).SkipAnd(Primary).Then(x => new TableRowModifier { IsOffset = true, Value = x })
                            )))
                        .And(AnyTagsList)
                        .AndSkip(CreateTag("endtablerow").ElseError($"'{{% endtablerow %}}' was expected"))
                        .Then<Statement>(x =>
                        {
                            var identifier = x.Item1;
                            var collection = x.Item2;
                            var statements = x.Item4;
                            var (colsResult, limitResult, offsetResult) = ReadTableRowStatementConfiguration(x.Item3);
                            return new TableRowStatement(statements, identifier, collection, limitResult, offsetResult, colsResult);
                        })
                    .ElseError("Invalid 'tablerow' tag");
        TableRowTag.Name = "TableRowTag";

        Parser<Statement> LiquidChild = _liquidRegisteredTag;

        if (_trackStatementLocations)
        {
            // In liquid mode TagStart consumes nothing, but preserves the start before Switch parses the arguments.
            LiquidChild = TrackStatement(TagStart.SkipAnd(LiquidChild), skipLeadingWhitespace: true);
        }

        var LiquidTag = Literals.WhiteSpace(true) // {% liquid %} can start with new lines
            .Then((context, x) =>
            {
                var ctx = (FluidParseContext)context;
                ctx.LiquidTagDepth++;
                return x;
            })
            .SkipAnd(ZeroOrMany(LiquidChild))
            .Then((context, x) =>
            {
                var ctx = (FluidParseContext)context;
                ctx.LiquidTagDepth--;
                return x;
            })
            .AndSkip(If<FluidParseContext, TagResult>(
                static context => context.LiquidTagDepth == 0,
                TagEnd,
                Always<TagResult>()))
            .Then<Statement>(x => new LiquidStatement(x))
            ;
        LiquidTag.Name = "LiquidTag";

        var EchoTag = FilterExpression.Else(EmptyKeyword).AndSkip(TagEnd).Then<Statement>(x => new OutputStatement(x));
        EchoTag.Name = "EchoTag";

        RegisteredTags["break"] = BreakTag;
        RegisteredTags["continue"] = ContinueTag;
        RegisteredTags["comment"] = CommentTag;
        RegisteredTags["#"] = InlineCommentTag;
        RegisteredTags["doc"] = DocTag;
        RegisteredTags["capture"] = CaptureTag;
        RegisteredTags["cycle"] = CycleTag;
        RegisteredTags["decrement"] = DecrementTag;
        RegisteredTags["ifchanged"] = IfChangedTag;
        RegisteredTags["include"] = IncludeTag;
        RegisteredTags["render"] = RenderTag;
        RegisteredTags["increment"] = IncrementTag;
        RegisteredTags["raw"] = RawTag;
        RegisteredTags["assign"] = AssignTag;
        RegisteredTags["if"] = IfTag;
        RegisteredTags["unless"] = UnlessTag;
        RegisteredTags["case"] = CaseTag;
        RegisteredTags["for"] = ForTag;
        RegisteredTags["tablerow"] = TableRowTag;
        RegisteredTags["liquid"] = LiquidTag;
        RegisteredTags["echo"] = EchoTag;

        if (parserOptions.AllowFunctions)
        {
            RegisteredTags["macro"] = MacroTag;
            RegisteredTags["from"] = FromTag;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static (Expression limitResult, Expression offsetResult) ReadForStatementConfiguration(IReadOnlyList<ForModifier> modifiers)
        {
            if (modifiers.Count == 0)
            {
                return (null, null);
            }

            // take slower route when needed
            static (Expression limitResult, Expression offsetResult) ReadFromList(IReadOnlyList<ForModifier> modifiers)
            {
                Expression limitResult = null;
                Expression offsetResult = null;
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
                }

                return (limitResult, offsetResult);
            }


            return ReadFromList(modifiers);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static (Expression colsResult, Expression limitResult, Expression offsetResult) ReadTableRowStatementConfiguration(IReadOnlyList<TableRowModifier> modifiers)
        {
            if (modifiers.Count == 0)
            {
                return (null, null, null);
            }

            static (Expression colsResult, Expression limitResult, Expression offsetResult) ReadFromList(IReadOnlyList<TableRowModifier> modifiers)
            {
                Expression colsResult = null;
                Expression limitResult = null;
                Expression offsetResult = null;
                for (var i = modifiers.Count - 1; i > -1; --i)
                {
                    var l = modifiers[i];
                    if (l.IsCols && colsResult is null)
                    {
                        colsResult = l.Value;
                    }

                    if (l.IsLimit && limitResult is null)
                    {
                        limitResult = l.Value;
                    }

                    if (l.IsOffset && offsetResult is null)
                    {
                        offsetResult = l.Value;
                    }
                }

                return (colsResult, limitResult, offsetResult);
            }

            return ReadFromList(modifiers);
        }

        var AnyTags = TrackStatement(TagStart.SkipAnd(_anyRegisteredTag), skipLeadingWhitespace: true);
        var KnownTags = TrackStatement(TagStart.SkipAnd(_knownRegisteredTag), skipLeadingWhitespace: true);

        RefreshRegisteredTagParser();
        AnyTagsList.Parser = ZeroOrMany(Output.Or(AnyTags).Or(Text)); // Used in block and stop when an unknown tag is found
        KnownTagsList.Parser = ZeroOrMany(Output.Or(KnownTags).Or(Text)); // Used in main list and raises an issue when an unknown tag is found

        Grammar = Always().Then(x =>
        {
            RefreshRegisteredTagParser();
            return x;
        }).SkipAnd(KnownTagsList).AndSkip(Eof().ElseError(ErrorMessages.UnexpectedTrailingInput));
    }

    /// <summary>
    /// Creates a parser for a tag without arguments, like an end tag. Like in Shopify, the tag ignores anything after its name: {% endif anything %}
    /// </summary>
    public Parser<string> CreateTag(string tagName) => TagStart.SkipAnd(Word(tagName)).AndSkip(_tagEndIgnoringMarkup);

    /// <summary>
    /// Matches a Liquid word, like an operator, a tag argument or a tag name, when no word char ([A-Za-z0-9_]) follows it: "or" doesn't match "orb".
    /// </summary>
    /// <param name="word">The word to match.</param>
    protected static Parser<string> Word(string word) => Terms.Text(word).WhenNotFollowedBy(WordChar);

    /// <summary>
    /// Creates the statement of a text, with the trimming requested by the delimiters around it: a text is always
    /// between the end of a tag or an output ('%}', '}}', or '-%}', '-}}' to strip it) and the start of the next one
    /// ('{%', '{{', or '{%-', '{{-').
    /// </summary>
    private static TextSpanStatement CreateTextSpanStatement(in TextSpan text)
    {
        var result = new TextSpanStatement(text);
        var buffer = text.Buffer;
        var start = text.Offset;
        var end = start + text.Length;

        if (start >= 2 && buffer[start - 1] == '}')
        {
            result.PreviousIsTag = buffer[start - 2] == '%';
            result.PreviousIsOutput = buffer[start - 2] == '}';
            result.StripLeft = start >= 3 && buffer[start - 3] == '-' && (result.PreviousIsTag || result.PreviousIsOutput);
        }

        if (end + 1 < buffer.Length && buffer[end] == '{')
        {
            result.NextIsTag = buffer[end + 1] == '%';
            result.NextIsOutput = buffer[end + 1] == '{';
            result.StripRight = end + 2 < buffer.Length && buffer[end + 2] == '-' && (result.NextIsTag || result.NextIsOutput);
        }

        return result;
    }

    private void RefreshRegisteredTagParser()
    {
        lock (_registeredTagLock)
        {
            if (RegisteredTags.Count == _registeredTagSnapshot.Length)
            {
                var i = 0;
                foreach (var tag in RegisteredTags)
                {
                    if (tag.Key != _registeredTagSnapshot[i].Key || tag.Value != _registeredTagSnapshot[i].Value)
                    {
                        break;
                    }

                    i++;
                }

                if (i == _registeredTagSnapshot.Length)
                {
                    return;
                }
            }

            RebuildRegisteredTagParsers();
        }
    }

    private void RebuildRegisteredTagParsers()
    {
        var tags = RegisteredTags.ToArray();
        var indices = new Dictionary<string, int>(tags.Length, RegisteredTags.Comparer);
        var parsers = new Parser<Statement>[tags.Length];

        for (var i = 0; i < tags.Length; i++)
        {
            indices.Add(tags[i].Key, i);
            parsers[i] = tags[i].Value;
        }

        var requiredTagName = _tagName.ElseError(ErrorMessages.IdentifierAfterTagStart);

        // Tag names are looked up without allocating a string: end tags, else and elsif are looked up and rejected
        // by every block body before the block reads them.
#if NET9_0_OR_GREATER
        var lookup = indices.GetAlternateLookup<ReadOnlySpan<char>>();
        int IndexOf(in TextSpan name) => lookup.TryGetValue(name.Span, out var index) ? index : -1;
#else
        int IndexOf(in TextSpan name) => indices.TryGetValue(name.ToString(), out var index) ? index : -1;
#endif

        int KnownTagIndex(ParseContext context, TextSpan name)
        {
            var index = IndexOf(name);

            if (index >= 0)
            {
                return index;
            }

            throw new ParseException($"Unknown tag '{name}' at {context.Scanner.Cursor.Position}",
                context.Scanner.Cursor.Buffer, context.Scanner.Cursor.Position);
        }

        _anyRegisteredTag.Parser = requiredTagName.Switch(
            (context, name) => IndexOf(name), parsers);
        _knownRegisteredTag.Parser = requiredTagName.Switch(KnownTagIndex, parsers);
        _liquidRegisteredTag.Parser = _tagName.Switch(KnownTagIndex, parsers);
        _registeredTagSnapshot = tags;
    }

    /// <summary>
    /// Records the source range of a statement parser when location tracking is enabled.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.Experimental("FLUID001")]
    protected Parser<T> TrackStatement<T>(Parser<T> parser, bool skipLeadingWhitespace = false) where T : Statement
    {
        if (!_trackStatementLocations)
        {
            return parser;
        }

        return parser.Then((context, start, end, statement) =>
        {
            if (skipLeadingWhitespace)
            {
                var buffer = context.Scanner.Buffer;
                while (start < end && LiquidWhiteSpace.IsWhiteSpace(buffer[start]))
                {
                    start++;
                }
            }

            statement.SourceOffset = start;
            statement.SourceLength = context.Scanner.Cursor.Offset - start;
            return statement;
        });
    }

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
}
