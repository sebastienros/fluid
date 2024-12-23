using Fluid.Values;

namespace Fluid.Ast.BinaryExpressions
{
    public sealed class AndBinaryExpression : BinaryExpression
    {
        public AndBinaryExpression(Expression left, Expression right) : base(left, right)
        {
        }

        internal override FluidValue Evaluate(FluidValue leftValue, FluidValue rightValue, TemplateContext context)
        {
            return BooleanValue.Create(leftValue.ToBooleanValue() && rightValue.ToBooleanValue());
        }

        protected internal override Expression Accept(AstVisitor visitor) => visitor.VisitAndBinaryExpression(this);
    }
}
