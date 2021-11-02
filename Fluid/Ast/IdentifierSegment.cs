using Fluid.Values;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Fluid.Ast
{
    [DebuggerDisplay("{Identifier,nq}")]
    public class IdentifierSegment : MemberSegment
    {
        public IdentifierSegment(string identifier)
        {
            Identifier = identifier;
        }

        public string Identifier { get; }

        public override ValueTask<FluidValue> ResolveAsync(FluidValue value, TemplateContext context)
        {
            return value.GetValueAsync(Identifier, context);
        }
    }
}
