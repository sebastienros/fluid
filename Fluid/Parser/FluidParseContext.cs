using Fluid.Ast;
using Parlot;
using Parlot.Fluent;

namespace Fluid.Parser
{
    public class FluidParseContext : ParseContext
    {
        public FluidParseContext(string text) : base(new Scanner(text))
        {
        }

        public TextSpanStatement PreviousTextSpanStatement { get; set; }
        public bool StripNextTextSpanStatement { get; set; }
        public bool PreviousIsTag { get; set; }
        public bool PreviousIsOutput { get; set; }
    }
}
