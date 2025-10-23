using Fluid.Values;

namespace Fluid.Ast.BinaryExpressions
{
    public sealed class OrBinaryExpression : BinaryExpression
    {
        public OrBinaryExpression(Expression left, Expression right) : base(left, right)
        {
        }

        internal override FluidValue Evaluate(FluidValue leftValue, FluidValue rightValue)
        {
            return leftValue;
        }

        protected internal override Expression Accept(AstVisitor visitor) => visitor.VisitOrBinaryExpression(this);
    }
}
