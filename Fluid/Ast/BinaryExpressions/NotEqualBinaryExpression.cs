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
            bool comparisonResult;

            // When comparing number with string, always return true (regardless of order)
            // This ensures symmetry: "1" != 1 returns true, and 1 != "1" also returns true
            if ((leftValue is NumberValue && rightValue is StringValue) ||
                (leftValue is StringValue && rightValue is NumberValue))
            {
                comparisonResult = true;
            }
            else
            {
                // For all other cases, use the default Equals implementation
                comparisonResult = !leftValue.Equals(rightValue);
            }

            return new BinaryExpressionFluidValue(leftValue, comparisonResult);
        }

        protected internal override Expression Accept(AstVisitor visitor) => visitor.VisitNotEqualBinaryExpression(this);
    }
}
