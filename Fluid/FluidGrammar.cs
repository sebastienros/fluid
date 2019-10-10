using Irony.Parsing;

namespace Fluid
{
    [Language("Fluid", "0.2", "Liquid based syntax")]
    public class FluidGrammar : Grammar
    {
        public IdentifierTerminal Identifier = new IdentifierTerminal("identifier", "-_", "_");
        public NonTerminal MemberAccess = new NonTerminal("memberAccess");
        public NonTerminal MemberAccessSegmentOpt = new NonTerminal("memberAccessSegmentOpt");
        public NonTerminal MemberAccessSegment = new NonTerminal("memberAccessSegment");
        public NonTerminal MemberAccessSegmentIdentifier = new NonTerminal("memberAccessSegmentIdentifier");
        public NonTerminal MemberAccessSegmentIndexer = new NonTerminal("memberAccessSegmentIndexer");
        public NonTerminal Statement = new NonTerminal("statement");
        public NonTerminal Output = new NonTerminal("output");
        public NonTerminal Tag = new NonTerminal("tag");
        public NonTerminal FilterList = new NonTerminal("filterList");
        public NonTerminal Filter = new NonTerminal("filter");
        public NonTerminal Expression = new NonTerminal("expression");
        public NonTerminal Term = new NonTerminal("term");
        public NonTerminal BinaryExpression = new NonTerminal("binaryExpression");
        public NonTerminal BinaryOperator = new NonTerminal("binaryOperator");
        public NonTerminal FilterArguments = new NonTerminal("filterArguments");
        public NonTerminal FilterArgument = new NonTerminal("filterArgument");
        public NonTerminal CycleArguments = new NonTerminal("cycleArguments");
        public NonTerminal CycleArgument = new NonTerminal("cycleArgument");
        public NonTerminal Boolean = new NonTerminal("boolean");
        public NonTerminal KnownTags = new NonTerminal("knownTags");

        public NonTerminal If = new NonTerminal("if");
        public NonTerminal Elsif = new NonTerminal("elsif");
        public NonTerminal Unless = new NonTerminal("unless");
        
        public NonTerminal Case = new NonTerminal("case");
        public NonTerminal When = new NonTerminal("when");
        public NonTerminal TermList = new NonTerminal("termList");

        public NonTerminal For = new NonTerminal("for");
        public NonTerminal ForSource = new NonTerminal("forSource");
        public NonTerminal ForOptions = new NonTerminal("forOptions");
        public NonTerminal ForOption = new NonTerminal("forOption");
        public NonTerminal ForLimit = new NonTerminal("limit");
        public NonTerminal ForOffset = new NonTerminal("offset");
        public NonTerminal Range = new NonTerminal("range");
        public NonTerminal RangeIndex = new NonTerminal("rangeIndex");

        public NonTerminal Cycle = new NonTerminal("cycle");
        public NonTerminal Assign = new NonTerminal("assign");
        public NonTerminal Capture = new NonTerminal("capture");
        public NonTerminal Increment = new NonTerminal("increment");
        public NonTerminal Decrement = new NonTerminal("decrement");
        
        public NonTerminal Include = new NonTerminal("include");
        public NonTerminal IncludeAssignments = new NonTerminal("includeAssignments");
        public NonTerminal IncludeAssignment = new NonTerminal("includeAssignment");

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
            var StringLiteralSingle = new StringLiteral("string1", "'", StringOptions.AllowsDoubledQuote | StringOptions.AllowsAllEscapes | StringOptions.AllowsLineBreak);
            var StringLiteralDouble = new StringLiteral("string2", "\"", StringOptions.AllowsDoubledQuote | StringOptions.AllowsAllEscapes | StringOptions.AllowsLineBreak);
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
            BinaryOperator.Rule = ToTerm("+") | ToTerm("-") | ToTerm("*") | ToTerm("/") | ToTerm("%")
                       | ToTerm("==") | ToTerm(">") | ToTerm("<") | ToTerm(">=") | ToTerm("<=") | ToTerm("<>") | ToTerm("!=") | ToTerm("contains")
                       | ToTerm("and") | ToTerm("or");
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
                Increment | Decrement |
                Include;

            If.Rule = ToTerm("if") + Expression;
            Unless.Rule = ToTerm("unless") + Expression;
            Elsif.Rule = ToTerm("elsif") + Expression;

            Case.Rule = ToTerm("case") + Expression;
            When.Rule = ToTerm("when") + TermList;
            TermList.Rule = MakePlusRule(TermList, ToTerm("or"), Term);

            For.Rule = ToTerm("for") + Identifier + ToTerm("in") + ForSource + ForOptions;
            ForSource.Rule = MemberAccess | Range;
            Range.Rule = ToTerm("(") + RangeIndex + ToTerm("..") + RangeIndex + ToTerm(")");
            RangeIndex.Rule = Number | MemberAccess;
            ForOptions.Rule = MakeStarRule(ForOptions, ForOption);
            ForOption.Rule = ForLimit | ForOffset | ToTerm("reversed");
            ForOffset.Rule = ToTerm("offset") + Colon + Number;
            ForLimit.Rule = ToTerm("limit") + Colon + Number;

            Cycle.Rule = ToTerm("cycle") + Term + Colon + CycleArguments;
            Cycle.Rule |= ToTerm("cycle") + CycleArguments;
            CycleArguments.Rule = MakePlusRule(CycleArguments, Comma, Term);

            Assign.Rule = ToTerm("assign") + Identifier + ToTerm("=") + Expression;
            Increment.Rule = ToTerm("increment") + Identifier;
            Decrement.Rule = ToTerm("decrement") + Identifier;

            Capture.Rule = ToTerm("capture") + Identifier;

            Include.Rule = (ToTerm("include") + Term) | (ToTerm("include") + Term + ToTerm("with") + Term) | (ToTerm("include") + Term + Comma + IncludeAssignments);
            IncludeAssignments.Rule = (IncludeAssignments + Comma + IncludeAssignment) | IncludeAssignment;
            IncludeAssignment.Rule = Identifier + Colon + Term;

            MarkPunctuation(
                "[", "]", ":", "|", "=",
                "if", "elsif", "unless", "assign", "capture",
                "increment", "decrement",
                "case",
                "for", "in", "(", ")", "..",
                "when", "cycle", "limit", "offset",
                "include", "with"
                );
            MarkPunctuation(Dot, TagStart, TagEnd, OutputStart, OutputEnd, Colon);
            MarkTransient(Statement, KnownTags, ForSource, RangeIndex, BinaryOperator, ForOption, Term);
        }

        public override void SkipWhitespace(ISourceStream source)
        {
            // This method is a copy of the base one with the additiong of the non-breaking space
            // c.f. https://github.com/sebastienros/fluid/issues/138

            const char NonBreakingSpace = (char)160;

            while (!source.EOF())
            {
                switch (source.PreviewChar)
                {
                    case ' ':
                    case '\t':
                    case NonBreakingSpace:
                        break;
                    case '\r':
                    case '\n':
                    case '\v':
                        if (UsesNewLine) return; //do not treat as whitespace if language is line-based
                        break;
                    default:
                        return;
                }
                source.PreviewPosition++;
            }
        }
    }
}
