using Fluid.Values;

namespace Fluid.Ast.BinaryExpressions
{
    public sealed class MultiplyBinaryExpression : BinaryExpression
    {
        public MultiplyBinaryExpression(Expression left, Expression right) : base(left, right)
        {
        }

        internal override FluidValue Evaluate(FluidValue leftValue, FluidValue rightValue)
        {
            return leftValue is NumberValue && rightValue is NumberValue
                ? NumberValue.Create(leftValue.ToNumberValue() * rightValue.ToNumberValue())
                : NilValue.Instance;
        }
    }
}
