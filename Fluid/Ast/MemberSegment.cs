using System.Threading.Tasks;
using Fluid.Values;

namespace Fluid.Ast
{
    public abstract class MemberSegment
    {
        public abstract ValueTask<FluidValue> ResolveAsync(FluidValue value, TemplateContext context);
        public abstract ValueTask<FluidValue> ResolveAsync(Scope value, TemplateContext context);
    }
}
