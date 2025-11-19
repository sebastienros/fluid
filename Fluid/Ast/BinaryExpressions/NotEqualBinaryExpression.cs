using Fluid.Values;

namespace Fluid.Ast.BinaryExpressions
{
    public sealed class NotEqualBinaryExpression : BinaryExpression
    {
        public NotEqualBinaryExpression(Expression left, Expression right) : base(left, right)
        {
        }

        internal override FluidValue Evaluate(FluidValue leftValue, FluidValue rightValue)
        {
            var comparisonResult = !leftValue.Equals(rightValue);
            return new BinaryExpressionFluidValue(leftValue, comparisonResult);
        }

        protected internal override Expression Accept(AstVisitor visitor) => visitor.VisitNotEqualBinaryExpression(this);
    }
}
