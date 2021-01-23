using System.Threading.Tasks;
using Fluid.Values;

namespace Fluid.Ast
{
    public abstract class Expression
    {
        public abstract ValueTask<FluidValue> EvaluateAsync(TemplateContext context);
    }
}