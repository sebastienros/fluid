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
            var Identifier = new IdentifierTerminal("identifier");
            var Pipe = ToTerm("|");
            var Colon = ToTerm(":");
            var StringLiteralSingle = new StringLiteral("string1", "'", StringOptions.AllowsDoubledQuote | StringOptions.AllowsAllEscapes);
            var StringLiteralDouble = new StringLiteral("string2", "\"", StringOptions.AllowsDoubledQuote | StringOptions.AllowsAllEscapes);
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
            var Term = new NonTerminal("term");
            var BinaryExpression = new NonTerminal("binaryExpression");
            var BinaryOperator = new NonTerminal("binaryOperator");
            var FilterArguments = new NonTerminal("filterArguments");
            var FilterArgument = new NonTerminal("filterArgument");
            var CycleArguments = new NonTerminal("cycleArguments");
            var CycleArgument = new NonTerminal("cycleArgument");
            var Boolean = new NonTerminal("boolean");
            var KnownTags = new NonTerminal("knownTags");

            this.Root = Statement;

            // Statements
            Statement.Rule = Output | Tag;
            Output.Rule = OutputStart + Expression + OutputEnd;
            Tag.Rule = TagStart + KnownTags + TagEnd;

            // Members
            MemberAccess.Rule = Identifier + MemberAccessSegmentOpt;
            MemberAccessSegmentOpt.Rule = MakeStarRule(MemberAccessSegmentOpt, MemberAccessSegment);
            MemberAccessSegment.Rule = MemberAccessSegmentIdentifier | MemberAccessSegmentIndexer;
            MemberAccessSegmentIdentifier.Rule = Dot + Identifier;
            MemberAccessSegmentIndexer.Rule = "[" + Expression + "]";

            // Expression
            Expression.Rule = Term + FilterList;
            Expression.Rule |= BinaryExpression;
            Term.Rule = MemberAccess | StringLiteralSingle | StringLiteralDouble | Number | Boolean;
            BinaryExpression.Rule = Expression + BinaryOperator + Expression;
            BinaryOperator.Rule = ToTerm("+") | "-" | "*" | "/" | "%"
                       | "==" | ">" | "<" | ">=" | "<=" | "<>" | "!=" | "contains"
                       | "and" | "or";
            Boolean.Rule = True | False;

            // Operators
            RegisterOperators(10, "*", "/", "%");
            RegisterOperators(9, "+", "-");
            RegisterOperators(8, "==", ">", "<", ">=", "<=", "<>", "!=", "contains");
            RegisterOperators(5, "and");
            RegisterOperators(4, "or");

            // Filters
            FilterList.Rule = MakeStarRule(FilterList, Filter);
            Filter.Rule = Pipe + Identifier;
            Filter.Rule |= Pipe + Identifier + Colon + FilterArguments;
            FilterArguments.Rule = MakeListRule(FilterArguments, Comma, FilterArgument);
            FilterArgument.Rule = Identifier + Colon + Term;
            FilterArgument.Rule |= Term;

            // Known Tags
            var If = new NonTerminal("if");
            var EndIf = ToTerm("endif");

            var Else = ToTerm("else");
            var Elsif = new NonTerminal("elsif");
            var Unless = new NonTerminal("unless");
            var EndUnless = ToTerm("endunless");

            var Case = new NonTerminal("case");
            var EndCase = ToTerm("endcase");
            var When = new NonTerminal("when");
            var TermList = new NonTerminal("termList");

            var For = new NonTerminal("for");
            var EndFor = ToTerm("endfor");
            var ForSource = new NonTerminal("forSource");
            var ForOptions = new NonTerminal("forOptions");
            var ForOption = new NonTerminal("forOption");
            var ForLimit = new NonTerminal("limit");
            var ForOffset = new NonTerminal("offset");
            var Range = new NonTerminal("range");
            var RangeIndex = new NonTerminal("rangeIndex");

            var Cycle = new NonTerminal("cycle");
            var Assign = new NonTerminal("assign");
            var Capture = new NonTerminal("capture");
            var EndCapture = ToTerm("endcapture");

            var Continue = ToTerm("continue");
            var Break = ToTerm("break");
            var Comment = ToTerm("comment");
            var EndComment = ToTerm("endcomment");
            var Raw = ToTerm("raw");
            var EndRaw = ToTerm("endraw");

            KnownTags.Rule =
                If | EndIf |
                Elsif | Else |
                Unless | EndUnless |
                Case | EndCase | When |
                For | EndFor |
                Continue | Break |
                Comment | EndComment |
                Raw | EndRaw |
                Cycle | Assign | Capture | EndCapture;

            If.Rule = "if" + Expression;
            Unless.Rule = "unless" + Expression;
            Elsif.Rule = "elsif" + Expression;

            Case.Rule = "case" + Expression;
            When.Rule = "when" + TermList;
            TermList.Rule = MakePlusRule(TermList, ToTerm("or"), Term);

            For.Rule = "for" + Identifier + "in" + ForSource + ForOptions;
            ForSource.Rule = MemberAccess | Range;
            Range.Rule = "(" + RangeIndex + ".." + RangeIndex + ")";
            RangeIndex.Rule = Number | MemberAccess;
            ForOptions.Rule = MakeStarRule(ForOptions, ForOption);
            ForOption.Rule = ForLimit | ForOffset | "reversed";
            ForOffset.Rule = "offset" + Colon + Number;
            ForLimit.Rule = "limit" + Colon + Number;

            Cycle.Rule = "cycle" + Term + Colon + CycleArguments;
            Cycle.Rule |= "cycle" + CycleArguments;
            CycleArguments.Rule = MakePlusRule(CycleArguments, Comma, Term);

            Assign.Rule = "assign" + Identifier + "=" + Expression;

            Capture.Rule = "capture" + Identifier;

            MarkPunctuation(
                "[", "]", ":", "|", "=",
                "if", "elsif", "unless", "assign", "capture",
                "case",
                "for", "in", "(", ")", "..",
                "when", "cycle", "limit", "offset"
                );
            MarkPunctuation(Dot, TagStart, TagEnd, OutputStart, OutputEnd, Colon);
            MarkTransient(Statement, KnownTags, ForSource, RangeIndex, BinaryOperator, ForOption, Term);
        }
    }
}
