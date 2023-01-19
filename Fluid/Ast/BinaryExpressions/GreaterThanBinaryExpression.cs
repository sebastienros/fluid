using Fluid.Values;

namespace Fluid.Ast.BinaryExpressions
{
    public sealed class GreaterThanBinaryExpression : BinaryExpression
    {
        public GreaterThanBinaryExpression(Expression left, Expression right, bool strict) : base(left, right)
        {
            Strict = strict;
        }

        public bool Strict { get; }

        protected internal override Expression Accept(AstVisitor visitor) => visitor.VisitGreaterThanBinaryExpression(this);

        internal override FluidValue Evaluate(FluidValue leftValue, FluidValue rightValue)
        {
            return IsGreater(leftValue, rightValue, Strict);
        }

        public static BooleanValue IsGreater(FluidValue leftValue, FluidValue rightValue, bool strict)
        {
            if (leftValue.IsNil() || rightValue.IsNil())
            {
                if (strict)
                {
                    return BooleanValue.False;
                }

                return leftValue.IsNil() && rightValue.IsNil()
                    ? BooleanValue.True
                    : BooleanValue.False;
            }

            if (leftValue is NumberValue)
            {
                if (strict)
                {
                    return leftValue.ToNumberValue() > rightValue.ToNumberValue()
                        ? BooleanValue.True
                        : BooleanValue.False;
                }

                return leftValue.ToNumberValue() >= rightValue.ToNumberValue()
                    ? BooleanValue.True
                    : BooleanValue.False;
            }

            return BooleanValue.False;
        }
    }
}