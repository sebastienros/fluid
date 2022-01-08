using Fluid.Values;

namespace Fluid.Ast.BinaryExpressions
{
    internal sealed class ContainsBinaryExpression : BinaryExpression
    {
        public ContainsBinaryExpression(Expression left, Expression right) : base(left, right)
        {
        }

        protected override FluidValue Evaluate(FluidValue leftValue, FluidValue rightValue)
        {
            return leftValue.Contains(rightValue)
                ? BooleanValue.True
                : BooleanValue.False;
        }
    }
}