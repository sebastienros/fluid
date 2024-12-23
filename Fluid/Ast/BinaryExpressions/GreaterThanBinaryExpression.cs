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

        public static FluidValue IsGreaterThan(FluidValue leftValue, FluidValue rightValue, bool strict)
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
            return NilValue.Instance;
        }

        internal override FluidValue Evaluate(FluidValue leftValue, FluidValue rightValue, TemplateContext context)
        {
            return IsGreaterThan(leftValue, rightValue, Strict);
        }

        protected internal override Expression Accept(AstVisitor visitor) => visitor.VisitGreaterThanBinaryExpression(this);
    }
}
