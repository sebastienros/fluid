using Fluid.Values;

namespace Fluid.Ast
{
    public abstract class Expression
    {
        public abstract FluidValue Evaluate(TemplateContext context);
    }
}
