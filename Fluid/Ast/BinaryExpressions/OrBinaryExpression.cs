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
            return BooleanValue.Create(leftValue.ToBooleanValue() || rightValue.ToBooleanValue());
        }
    }
}
