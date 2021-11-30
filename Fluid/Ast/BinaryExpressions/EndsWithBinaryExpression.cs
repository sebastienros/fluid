using Fluid.Values;

namespace Fluid.Ast.BinaryExpressions
{
    public sealed class EndsWithBinaryExpression : BinaryExpression
    {
        public EndsWithBinaryExpression(Expression left, Expression right) : base(left, right)
        {
        }

        internal override FluidValue Evaluate(FluidValue left, FluidValue right)
        {
            if (left is ArrayValue arrayValue)
            {
                var values = arrayValue.Values;
                var last = values.Length > 0 ? values[values.Length - 1] : NilValue.Instance;
                return last.Equals(right)
                        ? BooleanValue.True
                        : BooleanValue.False;
            }
            else
            {
                return left.ToStringValue().EndsWith(right.ToStringValue())
                        ? BooleanValue.True
                        : BooleanValue.False;
            }
        }
    }
}
