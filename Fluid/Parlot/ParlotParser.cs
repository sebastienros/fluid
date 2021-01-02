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
        protected static IParser<List<Statement>> Grammar;
        protected static Dictionary<string, IParser<Statement>> CustomTags { get; } = new();

        public bool TryParse(string template, bool stripEmptyLines, out List<Statement> result, out IEnumerable<string> errors)
        {
            var success = Grammar.TryParse(template, out result, out var parlotError);

            if (parlotError != null)
            {
                errors = new[] { $"{parlotError.Message} at {parlotError.Position}" };
            }
            else
            {
                errors = null;
            }

            return success;
        }

        static ParlotParser()
        {
            // TODO: Refactor such that ToList() is not necessary
            // TODO: Remove CommentStatement which is replaced by RawStatement

            var LBrace = Terms.Char('{');
            var RBrace = Terms.Char('}');
            var LBracket = Terms.Char('[');
            var RBracket = Terms.Char(']');
            var Equal = Terms.Char('=');
            var Colon = Terms.Char(':');
            var Comma = Terms.Char(',');
            var OutputStart = Literals.Text("{{");
            var OutputEnd = Terms.Text("}}");
            var TagStart = Literals.Text("{%");
            var TagEnd = Terms.Text("%}");
            var Dot = Literals.Char('.'); // No whitespace
            var Pipe = Terms.Char('|');

            var String = Terms.String(StringLiteralQuotes.SingleOrDouble);
            var Number = Terms.Decimal(NumberOptions.AllowSign);
            var True = Terms.Text("true");
            var False = Terms.Text("false");

            var divided = Terms.Char('/');
            var times = Terms.Char('*');
            var minus = Terms.Char('-');
            var plus = Terms.Char('+');
            var modulo = Terms.Char('%');
            var equals = Terms.Text("==");
            var notequals = Terms.Text("!=");
            var different = Terms.Text("<>");
            var greater = Terms.Text(">");
            var lower = Terms.Text("<");
            var greateror = Terms.Text(">=");
            var loweror = Terms.Text("<=");
            var contains = Terms.Text("contains");
            var or = Terms.Text("or");
            var and = Terms.Text("and");
            var openParen = Terms.Char('(');
            var closeParen = Terms.Char(')');


            var Identifier = Terms.Identifier(extraPart: static c => c == '-');

            // primary => NUMBER | STRING | BOOLEAN property
            var Primary =
                Number.Then<Expression>(x => new LiteralExpression(FluidValue.Create(x)))
                .Or(String.Then<Expression>(x => new LiteralExpression(FluidValue.Create(x.Text))))
                .Or(True.Then<Expression>(x => new LiteralExpression(BooleanValue.True)))
                .Or(False.Then<Expression>(x => new LiteralExpression(BooleanValue.False)))
                .Or(Identifier.Then<Expression>(x => new MemberExpression(new IdentifierSegment(x.Text))))
                ;

            var CaseValueList = Separated(or, Primary).Then(x => x.ToList());
            
            // TODO: 'and' has a higher priority than 'or', either create a new scope, or implement operators priority

            var Logical = Primary.And(Star(OneOf(or, and, contains).And(Primary)))
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

            var Comparison = Logical.And(Star(OneOf(equals, notequals, different, greateror, loweror, greater, lower).And(Logical)))
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

            // factor => primary ( ( "/" | "*" ) primary )* ;
            var Factor = Comparison.And(Star(divided.Or(times).And(Comparison)))
                .Then(static x =>
                {
                    var result = x.Item1;

                    // (("/" | "*") unary ) *
                    foreach (var op in x.Item2)
                    {
                        result = op.Item1 switch
                        {
                            '/' => new DivideBinaryExpression(result, op.Item2),
                            '%' => new ModuloBinaryExpression(result, op.Item2),
                            '*' => new MultiplyBinaryExpression(result, op.Item2),
                            _ => null
                        };
                    }

                    return result;
                });

            // expression => factor ( ( "-" | "+" ) factor )* ;
            var Expression = Factor.And(Star(plus.Or(minus).And(Factor)))
                .Then(static x =>
                {
                    var result = x.Item1;

                    // (("-" | "+") factor ) *
                    foreach (var op in x.Item2)
                    {
                        result = op.Item1 switch
                        {
                            '+' => new AddBinaryExpression(result, op.Item2),
                            '-' => new SubstractBinaryExpression(result, op.Item2),
                            _ => null
                        };
                    }

                    return result;
                });

            // | identifer [ ':' (name : value)+ ]
            var Filter = Pipe.And(Identifier
                .And(ZeroOrOne(Colon.And(
                    OneOrMany(
                        Identifier.And(Colon).And(Number).Then(static x => new FilterArgument(x.Item1.Text, new LiteralExpression(FluidValue.Create(x.Item3))))
                    )).Then(static x => x.Item2))));

            var TagsList = Deferred<List<Statement>>();

            var output = OutputStart.And(Expression).And(OutputEnd)
                .Then<Statement>(static x => new OutputStatement(x.Item2));

            var otherTags= Identifier.Switch((context, previous) =>
            {
                if (CustomTags.TryGetValue(previous.Text, out var parser))
                {
                    return parser;
                }

                return null;
            });

            IParser<ValueTuple<string, string, string>> CreateTag(string tagName) => TagStart.And(Terms.Text(tagName)).And(TagEnd);

            var knownTags = Identifier.Switch((context, previous) =>
            {
                // Because tags like 'else' are not listed, they won't count in TagsList, and will stop being processed 
                // as inner tags in blocks like {% if %} TagsList {% endif $}

                switch (previous.Text)
                {
                    case "break": return TagEnd.Then<Statement>(x => new BreakStatement());
                    case "continue": return TagEnd.Then<Statement>(x => new ContinueStatement());
                    case "capture": return 
                        Identifier
                        .And(TagEnd)
                        .And(TagsList)
                        .And(CreateTag("endcapture"))
                        .Then<Statement>(x => new CaptureStatement(x.Item1.Text, x.Item3));
                    case "cycle": return
                        ZeroOrOne(Colon.SkipAnd(String)).And(Separated(Comma, String))
                        .And(TagEnd)
                        .Then<Statement>(x => new CycleStatement(x.Item1.Length == 0 ? null : new LiteralExpression(FluidValue.Create(x.Item1.Text)), x.Item2.Select(e => new LiteralExpression(FluidValue.Create(e.Text))).ToList<Expression>()));
                    case "raw": return TagEnd.SkipAnd(AnyCharBefore(CreateTag("endraw"), consumeDelimiter: true, failOnEof: true).Then<Statement>(x => new RawStatement(x))).ElseError("Not end tag found for {% raw %}");
                    case "assign": return Identifier.And(Equal).And(Expression).And(TagEnd).Then<Statement>(x => new AssignStatement(x.Item1.Text, x.Item3));
                    case "if": return Expression
                        .And(TagEnd)
                        .And(TagsList)
                        .And(ZeroOrMany(
                            TagStart.And(Terms.Text("elsif")).And(Expression).And(TagEnd).And(TagsList))
                            .Then(x => x.Count > 0 ? x.Select(e => new ElseIfStatement(e.Item3, e.Item5)).ToList() : null))
                        .And(ZeroOrOne(
                            CreateTag("else").SkipAnd(TagsList))
                            .Then(x => x != null ? new ElseStatement(x) : null))
                        .And(CreateTag("endif"))
                        .Then<Statement>(x => new IfStatement(x.Item1, x.Item3, x.Item5, x.Item4));
                    case "unless": return Expression
                        .And(TagEnd)
                        .And(TagsList)
                        .And(CreateTag("endunless"))
                        .Then<Statement>(x => new UnlessStatement(x.Item1, x.Item3));
                    case "case": return Expression
                        .And(TagEnd)
                        .And(ZeroOrMany(
                            TagStart.And(Terms.Text("when")).And(CaseValueList).And(TagEnd).And(TagsList))
                            .Then(x => x.Count > 0 ? x.Select(e => new WhenStatement(e.Item3, e.Item5)).ToList() : null))
                        .And(ZeroOrOne(
                            CreateTag("else").SkipAnd(TagsList))
                            .Then(x => x != null ? new ElseStatement(x) : null))
                        .And(CreateTag("endcase"))
                        .Then<Statement>(x => new CaseStatement(x.Item1, x.Item4, x.Item3));
                    default: return null;
                };
            });

            var tag = TagStart.And(knownTags).Then(x => x.Item2);

            // todo: read the next - and assign the skip whitespace property to the adjacent Text statement
            var text = AnyCharBefore(OutputStart.Or(TagStart))
                .Then<Statement>(static x => new TextSpanStatement(x));

            TagsList.Parser = Star(output.Or(tag).Or(text)).Then(x => x.ToList());

            Grammar = TagsList;
        }
    }
}
