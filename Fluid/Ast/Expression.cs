using Fluid.Ast.Values;

namespace Fluid.Ast
{
    public abstract class Expression
    {
        public abstract FluidValue Evaluate(TemplateContext context);
    }
}
