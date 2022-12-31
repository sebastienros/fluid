using Fluid.Values;

namespace Fluid.Ast
{
    public abstract class Expression
    {
        public abstract ValueTask<FluidValue> EvaluateAsync(TemplateContext context);

        /// <summary>
        /// Returns whether the evaluation of the expression returns a constant value or not.
        /// </summary>
        /// <remarks>
        /// Use it to cache the value of the expression instead of re-evaluating it.
        /// </remarks>
        public virtual bool IsConstantExpression() => false;

        protected internal virtual Expression Accept(AstVisitor visitor) => visitor.VisitOtherExpression(this);
    }
}