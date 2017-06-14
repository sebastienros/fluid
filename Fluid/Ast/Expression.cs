using System.Threading.Tasks;
using Fluid.Values;

namespace Fluid.Ast
{
    public abstract class Expression
    {
        public abstract Task<FluidValue> EvaluateAsync(TemplateContext context);
    }
}
