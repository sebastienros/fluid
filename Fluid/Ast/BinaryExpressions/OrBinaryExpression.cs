using Fluid.Values;

namespace Fluid.Ast.BinaryExpressions
{
    public sealed class OrBinaryExpression : BinaryExpression
    {
        public OrBinaryExpression(Expression left, Expression right) : base(left, right)
        {
        }

        internal override FluidValue Evaluate(FluidValue leftValue, FluidValue rightValue, TemplateContext context)
        {
            return BooleanValue.Create(leftValue.ToBooleanValue() || rightValue.ToBooleanValue());
        }

        protected internal override Expression Accept(AstVisitor visitor) => visitor.VisitOrBinaryExpression(this);
    }
}
