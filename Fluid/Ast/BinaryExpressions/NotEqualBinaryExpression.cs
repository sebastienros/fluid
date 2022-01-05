using Fluid.Values;

namespace Fluid.Ast.BinaryExpressions
{
    internal sealed class NotEqualBinaryExpression : BinaryExpression
    {
        public NotEqualBinaryExpression(Expression left, Expression right) : base(left, right)
        {
        }

        protected override FluidValue Evaluate(FluidValue leftValue, FluidValue rightValue)
        {
            return leftValue.Equals(rightValue)
                ? BooleanValue.False
                : BooleanValue.True;
        }
    }
}
