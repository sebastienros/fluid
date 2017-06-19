using Irony.Parsing;

namespace Fluid.MvcViewEngine
{
    public class FluidViewGrammar : FluidGrammar
    {
        public FluidViewGrammar() : base()
        {
            var Layout = new NonTerminal("layout");
            Layout.Rule = "layout" + Term;
            
            var Section = new NonTerminal("section");
            Section.Rule = "section" + Identifier;
            var SectionEnd = ToTerm("endsection");

            var RenderSection = new NonTerminal("rendersection");
            RenderSection.Rule = "rendersection" + Identifier;

            var RenderBody = ToTerm("renderbody");

            KnownTags.Rule |= Layout 
                | Section | SectionEnd 
                | RenderSection
                | RenderBody;

            // Prevent the text from being added in the parsed tree.
            // Only Identifier and Range will be in the tree.
            MarkPunctuation("layout", "section", "rendersection");
        }
    }
}
