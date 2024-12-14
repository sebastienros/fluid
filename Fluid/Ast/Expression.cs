using Fluid.Values;

namespace Fluid.Ast
{
    public abstract class Expression
    {
        public abstract ValueTask<FluidValue> EvaluateAsync(TemplateContext context);

        protected internal virtual Expression Accept(AstVisitor visitor) => visitor.VisitOtherExpression(this);
    }
}