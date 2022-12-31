using Fluid.Values;

namespace Fluid.Ast.BinaryExpressions
{
    public sealed class NotEqualBinaryExpression : BinaryExpression
    {
        public NotEqualBinaryExpression(Expression left, Expression right) : base(left, right)
        {
        }

        protected internal override Expression Accept(AstVisitor visitor) => visitor.VisitNotEqualBinaryExpression(this);

        internal override FluidValue Evaluate(FluidValue leftValue, FluidValue rightValue)
        {
            return leftValue.Equals(rightValue)
                ? BooleanValue.False 
                : BooleanValue.True;
        }
    }
}
