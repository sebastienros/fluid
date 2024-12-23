using Fluid.Values;

namespace Fluid.Ast.BinaryExpressions
{
    public sealed class NotEqualBinaryExpression : BinaryExpression
    {
        public NotEqualBinaryExpression(Expression left, Expression right) : base(left, right)
        {
        }

        internal override FluidValue Evaluate(FluidValue leftValue, FluidValue rightValue, TemplateContext context)
        {
            return leftValue.Equals(rightValue)
                ? BooleanValue.False
                : BooleanValue.True;
        }

        protected internal override Expression Accept(AstVisitor visitor) => visitor.VisitNotEqualBinaryExpression(this);
    }
}
