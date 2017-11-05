using System.Threading.Tasks;
using Fluid.Values;

namespace Fluid.Ast
{
    public abstract class MemberSegment
    {
        public abstract Task<FluidValue> ResolveAsync(FluidValue value, TemplateContext context);
        public abstract Task<FluidValue> ResolveAsync(Scope value, TemplateContext context);
    }
}
