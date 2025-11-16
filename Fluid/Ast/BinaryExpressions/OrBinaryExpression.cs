using Fluid.Values;

namespace Fluid.Ast.BinaryExpressions
{
    public sealed class OrBinaryExpression : BinaryExpression
    {
        public OrBinaryExpression(Expression left, Expression right) : base(left, right)
        {
        }

        internal override FluidValue Evaluate(FluidValue leftValue, FluidValue rightValue)
        {
            var comparisonResult = leftValue.ToBooleanValue() || rightValue.ToBooleanValue();
            return new BinaryExpressionFluidValue(leftValue, comparisonResult);
        }

        protected internal override Expression Accept(AstVisitor visitor) => visitor.VisitOrBinaryExpression(this);
    }
}
