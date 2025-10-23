using Fluid.Values;

namespace Fluid.Ast.BinaryExpressions
{
    public sealed class AndBinaryExpression : BinaryExpression
    {
        public AndBinaryExpression(Expression left, Expression right) : base(left, right)
        {
        }

        internal override FluidValue Evaluate(FluidValue leftValue, FluidValue rightValue)
        {
            var comparisonResult = leftValue.ToBooleanValue() && rightValue.ToBooleanValue();
            return new BinaryExpressionFluidValue(leftValue, comparisonResult);
        }

        protected internal override Expression Accept(AstVisitor visitor) => visitor.VisitAndBinaryExpression(this);
    }
}