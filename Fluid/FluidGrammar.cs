using Irony.Parsing;

namespace Fluid
{
    [Language("Fluid", "0.2", "Liquid based syntax")]
    public class FluidGrammar : Grammar
    {
        protected IdentifierTerminal Identifier = new IdentifierTerminal("identifier");
        protected NonTerminal MemberAccess = new NonTerminal("memberAccess");
        protected NonTerminal MemberAccessSegmentOpt = new NonTerminal("memberAccessSegmentOpt");
        protected NonTerminal MemberAccessSegment = new NonTerminal("memberAccessSegment");
        protected NonTerminal MemberAccessSegmentIdentifier = new NonTerminal("memberAccessSegmentIdentifier");
        protected NonTerminal MemberAccessSegmentIndexer = new NonTerminal("memberAccessSegmentIndexer");
        protected NonTerminal Statement = new NonTerminal("statement");
        protected NonTerminal Output = new NonTerminal("output");
        protected NonTerminal Tag = new NonTerminal("tag");
        protected NonTerminal FilterList = new NonTerminal("filterList");
        protected NonTerminal Filter = new NonTerminal("filter");
        protected NonTerminal Expression = new NonTerminal("expression");
        protected NonTerminal Term = new NonTerminal("term");
        protected NonTerminal BinaryExpression = new NonTerminal("binaryExpression");
        protected NonTerminal BinaryOperator = new NonTerminal("binaryOperator");
        protected NonTerminal FilterArguments = new NonTerminal("filterArguments");
        protected NonTerminal FilterArgument = new NonTerminal("filterArgument");
        protected NonTerminal CycleArguments = new NonTerminal("cycleArguments");
        protected NonTerminal CycleArgument = new NonTerminal("cycleArgument");
        protected NonTerminal Boolean = new NonTerminal("boolean");
        protected NonTerminal KnownTags = new NonTerminal("knownTags");
 
        protected NonTerminal If = new NonTerminal("if");
        protected NonTerminal Elsif = new NonTerminal("elsif");
        protected NonTerminal Unless = new NonTerminal("unless");
 
        protected NonTerminal Case = new NonTerminal("case");
        protected NonTerminal When = new NonTerminal("when");
        protected NonTerminal TermList = new NonTerminal("termList");

        protected NonTerminal For = new NonTerminal("for");
        protected NonTerminal ForSource = new NonTerminal("forSource");
        protected NonTerminal ForOptions = new NonTerminal("forOptions");
        protected NonTerminal ForOption = new NonTerminal("forOption");
        protected NonTerminal ForLimit = new NonTerminal("limit");
        protected NonTerminal ForOffset = new NonTerminal("offset");
        protected NonTerminal Range = new NonTerminal("range");
        protected NonTerminal RangeIndex = new NonTerminal("rangeIndex");
 
        protected NonTerminal Cycle = new NonTerminal("cycle");
        protected NonTerminal Assign = new NonTerminal("assign");
        protected NonTerminal Capture = new NonTerminal("capture");
        protected NonTerminal Increment = new NonTerminal("increment");
        protected NonTerminal Decrement = new NonTerminal("decrement");
 
        public FluidGrammar() : base(caseSensitive: true)
        {
            // Terminals
            var OutputStart = ToTerm("{{");
            var OutputEnd = ToTerm("}}");
            var TagStart = ToTerm("{%");
            var TagEnd = ToTerm("%}");
            var Dot = ToTerm(".");
            var Comma = ToTerm(",");
            var Pipe = ToTerm("|");
            var Colon = ToTerm(":");
            var StringLiteralSingle = new StringLiteral("string1", "'", StringOptions.AllowsDoubledQuote | StringOptions.AllowsAllEscapes);
            var StringLiteralDouble = new StringLiteral("string2", "\"", StringOptions.AllowsDoubledQuote | StringOptions.AllowsAllEscapes);
            var Number = new NumberLiteral("number", NumberOptions.AllowSign);
            var True = ToTerm("true");
            var False = ToTerm("false");

            // Non-terminals

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
            var EndIf = ToTerm("endif");
            var Else = ToTerm("else");
            var EndUnless = ToTerm("endunless");
            var EndCase = ToTerm("endcase");
            var EndFor = ToTerm("endfor");
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
                Cycle | Assign | Capture | EndCapture |
                Increment | Decrement;

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
            Increment.Rule = "increment" + Identifier;
            Decrement.Rule = "decrement" + Identifier;

            Capture.Rule = "capture" + Identifier;

            MarkPunctuation(
                "[", "]", ":", "|", "=",
                "if", "elsif", "unless", "assign", "capture",
                "increment", "decrement",
                "case",
                "for", "in", "(", ")", "..",
                "when", "cycle", "limit", "offset"
                );
            MarkPunctuation(Dot, TagStart, TagEnd, OutputStart, OutputEnd, Colon);
            MarkTransient(Statement, KnownTags, ForSource, RangeIndex, BinaryOperator, ForOption, Term);
        }
    }
}
