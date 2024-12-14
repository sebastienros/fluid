using Fluid.Values;

namespace Fluid.Ast.BinaryExpressions
{
    public sealed class ContainsBinaryExpression : BinaryExpression
    {
        public ContainsBinaryExpression(Expression left, Expression right) : base(left, right)
        {
        }

        protected internal override Expression Accept(AstVisitor visitor) => visitor.VisitContainsBinaryExpression(this);

        internal override FluidValue Evaluate(FluidValue leftValue, FluidValue rightValue)
        {
            return leftValue.Contains(rightValue)
                ? BooleanValue.True
                : BooleanValue.False;
        }

        protected internal override Expression Accept(AstVisitor visitor) => visitor.VisitContainsBinaryExpression(this);
    }
}