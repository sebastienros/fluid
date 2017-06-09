using Irony.Parsing;

namespace Fluid
{
    [Language("Fluid", "0.2", "Liquid based syntax")]
    public class FluidGrammar : Grammar
    {
        public FluidGrammar() : base(caseSensitive: true)
        {
            // Terminals
            var OutputStart = ToTerm("{{");
            var OutputEnd = ToTerm("}}");
            var TagStart = ToTerm("{%");
            var TagEnd = ToTerm("%}");
            var Dot = ToTerm(".");
            var Comma = ToTerm(",");
            var IdentifierPart = new IdentifierTerminal("identifier");
            var Pipe = ToTerm("|");
            var Colon = ToTerm(":");
            var StringLiteral = new StringLiteral("string", "'", StringOptions.AllowsDoubledQuote | StringOptions.AllowsAllEscapes);
            var Number = new NumberLiteral("number", NumberOptions.AllowSign);
            var True = ToTerm("true");
            var False = ToTerm("false");

            // Non-terminals
            var MemberAccess = new NonTerminal("memberAccess");
            var MemberAccessSegmentOpt = new NonTerminal("memberAccessSegmentOpt");
            var MemberAccessSegment = new NonTerminal("memberAccessSegment");
            var MemberAccessSegmentIdentifier = new NonTerminal("memberAccessSegmentIdentifier");
            var MemberAccessSegmentIndexer = new NonTerminal("memberAccessSegmentIndexer");
            var Statement = new NonTerminal("statement");
            var Output = new NonTerminal("output");
            var Tag = new NonTerminal("tag");
            var FilterList = new NonTerminal("filterList");
            var Filter = new NonTerminal("filter");
            var Expression = new NonTerminal("expression");
            var Literal = new NonTerminal("literal");
            var BinaryExpression = new NonTerminal("binaryExpression");
            var BinaryOperator = new NonTerminal("binaryOperator");
            var FilterArguments = new NonTerminal("filterArguments");
            var FilterArgument = new NonTerminal("filterArgument");
            var Boolean = new NonTerminal("boolean");
            var KnownTags = new NonTerminal("knownTags");

            this.Root = Statement;

            // Statements
            Statement.Rule = Output | Tag;
            Output.Rule = OutputStart + Expression + FilterList + OutputEnd;
            Tag.Rule = TagStart + KnownTags + TagEnd;

            // Members
            MemberAccess.Rule = IdentifierPart + MemberAccessSegmentOpt;
            MemberAccessSegmentOpt.Rule = MakeStarRule(MemberAccessSegmentOpt, MemberAccessSegment);
            MemberAccessSegment.Rule = MemberAccessSegmentIdentifier | MemberAccessSegmentIndexer;
            MemberAccessSegmentIdentifier.Rule = Dot + IdentifierPart;
            MemberAccessSegmentIndexer.Rule = "[" + Expression + "]";

            // Expression
            Expression.Rule = MemberAccess | Literal | BinaryExpression;
            Literal.Rule = StringLiteral | Number | Boolean;
            BinaryExpression.Rule = Expression + BinaryOperator + Expression;
            BinaryOperator.Rule = ToTerm("+") | "-" | "*" | "/" | "%"
                       | "==" | ">" | "<" | ">=" | "<=" | "<>" | "!=" | "!<" | "!>" | "contains"
                       | "and" | "or";
            Boolean.Rule = True | False;

            // Operators
            RegisterOperators(10, "*", "/", "%");
            RegisterOperators(9, "+", "-");
            RegisterOperators(8, "==", ">", "<", ">=", "<=", "<>", "!=", "!<", "!>", "contains");
            RegisterOperators(5, "and");
            RegisterOperators(4, "or");

            // Filters
            FilterList.Rule = MakeStarRule(FilterList, Filter);
            Filter.Rule = Pipe + IdentifierPart;
            Filter.Rule |= Pipe + IdentifierPart + Colon + FilterArguments;
            FilterArguments.Rule = MakeListRule(FilterArguments, Comma, FilterArgument);
            FilterArgument.Rule = StringLiteral | Number | Boolean | MemberAccess; // We are not using Expression here to limit the values that can be passed

            // Known Tags
            var If = new NonTerminal("if");
            var EndIf = new NonTerminal("endIf");

            var Else = new NonTerminal("else");
            var Elsif = new NonTerminal("elsif");
            var Unless = new NonTerminal("unless");
            var EndUnless = new NonTerminal("endUnless");

            var Case = new NonTerminal("case");
            var EndCase = new NonTerminal("endCase");
            var When = new NonTerminal("when");
            var LiteralList = new NonTerminal("literalList");

            var For = new NonTerminal("for");
            var EndFor = new NonTerminal("endFor");
            var ForSource = new NonTerminal("forSource");
            var Range = new NonTerminal("range");
            var RangeIndex = new NonTerminal("rangeIndex");
            var Continue = new NonTerminal("continue");
            var Break = new NonTerminal("break");

            KnownTags.Rule =
                If | EndIf |
                Elsif | Else |
                Unless | EndUnless |
                Case | EndCase | When |
                For | EndFor |
                Continue |
                Break;

            If.Rule = "if" + Expression;
            Elsif.Rule = "elsif";
            Else.Rule = "else";
            EndIf.Rule = "endif";

            Unless.Rule = "unless" + Expression;
            EndUnless.Rule = "endunless";

            Case.Rule = "case" + MemberAccess;
            When.Rule = "when" + LiteralList;
            LiteralList.Rule = MakePlusRule(LiteralList, ToTerm("or"), Literal);
            EndCase.Rule = "endcase";

            For.Rule = "for" + IdentifierPart + "in" + ForSource;
            ForSource.Rule = MemberAccess | Range;
            Range.Rule = "(" + RangeIndex + ".." + RangeIndex + ")";
            RangeIndex.Rule = Number | MemberAccess;
            EndFor.Rule = "endfor";

            Break.Rule = "break";
            Continue.Rule = "continue";

            MarkPunctuation(
                "[", "]", ":", "|",
                "if", "elseif", "endif",
                "case", "when", "endcase", "or",
                "for", "in", "endfor", "(", ")", "..",
                "unless", "endunless",
                "break", "continue", "else");
            MarkPunctuation(Dot, TagStart, TagEnd, OutputStart, OutputEnd, Colon);
            MarkTransient(Statement, KnownTags, ForSource, FilterArgument, RangeIndex);
        }
    }
}
