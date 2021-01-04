using Fluid.Ast;
using Parlot;
using Parlot.Fluent;

namespace Fluid.Parlot
{
    public class ParlotContext : ParseContext
    {
        public ParlotContext(string text) : base(new Scanner(text))
        {
        }

        public TextSpanStatement PreviousTextSpanStatement { get; set; }
        public bool StripNextTextSpanStatement { get; set; }
    }
}
