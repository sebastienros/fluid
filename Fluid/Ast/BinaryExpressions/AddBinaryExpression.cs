using Fluid.Values;

namespace Fluid.Ast.BinaryExpressions
{
    public sealed class AddBinaryExpression : BinaryExpression
    {
        public AddBinaryExpression(Expression left, Expression right) : base(left, right)
        {
        }

        internal override FluidValue Evaluate(FluidValue leftValue, FluidValue rightValue, TemplateContext context)
        {
            if (leftValue is StringValue)
            {
                return new StringValue(leftValue.ToStringValue(context) + rightValue.ToStringValue(context));
            }

            if (leftValue is NumberValue)
            {
                return NumberValue.Create(leftValue.ToNumberValue(context) + rightValue.ToNumberValue(context));
            }

            return NilValue.Instance;
        }

        protected internal override Expression Accept(AstVisitor visitor) => visitor.VisitAddBinaryExpression(this);
    }
}
