using Fluid.Values;

namespace Fluid.Ast.BinaryExpressions
{
    public sealed class AddBinaryExpression : BinaryExpression
    {
        public AddBinaryExpression(Expression left, Expression right) : base(left, right)
        {
        }

        internal override FluidValue Evaluate(FluidValue leftValue, FluidValue rightValue)
        {
            if (leftValue is StringValue)
            {
                return new StringValue(leftValue.ToStringValue() + rightValue.ToStringValue());
            }

            if (leftValue is NumberValue)
            {
                return NumberValue.Create(leftValue.ToNumberValue() + rightValue.ToNumberValue());
            }

            return NilValue.Instance;
        }
    }
}
