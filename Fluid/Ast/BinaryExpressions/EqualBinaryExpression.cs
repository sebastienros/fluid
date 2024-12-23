using Fluid.Values;

namespace Fluid.Ast.BinaryExpressions
{
    public sealed class EqualBinaryExpression : BinaryExpression
    {
        public EqualBinaryExpression(Expression left, Expression right) : base(left, right)
        {
        }

        internal override FluidValue Evaluate(FluidValue leftValue, FluidValue rightValue, TemplateContext context)
        {
            return leftValue.Equals(rightValue)
                ? BooleanValue.True
                : BooleanValue.False;
        }

        protected internal override Expression Accept(AstVisitor visitor) => visitor.VisitEqualBinaryExpression(this);
    }
}
