using Fluid.Ast;
using Parlot;
using Parlot.Fluent;

namespace Fluid.Parser
{
    internal sealed class FluidParseContext : ParseContext
    {
        public FluidParseContext(string text) : base(new Scanner(text))
        {
        }

        public TextSpanStatement PreviousTextSpanStatement { get; set; }
        public bool StripNextTextSpanStatement { get; set; }
        public bool PreviousIsTag { get; set; }
        public bool PreviousIsOutput { get; set; }
        public bool InsideLiquidTag { get; set; } // Used in the {% liquid %} tag to ensure a new line corresponds to '%}'
    }
}
