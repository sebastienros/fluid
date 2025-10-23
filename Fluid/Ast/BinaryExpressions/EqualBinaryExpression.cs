using Fluid.Values;

namespace Fluid.Ast.BinaryExpressions
{
    public sealed class EqualBinaryExpression : BinaryExpression
    {
        public EqualBinaryExpression(Expression left, Expression right) : base(left, right)
        {
        }

        internal override FluidValue Evaluate(FluidValue leftValue, FluidValue rightValue)
        {
            return leftValue;
        }

        protected internal override Expression Accept(AstVisitor visitor) => visitor.VisitEqualBinaryExpression(this);
    }
}
