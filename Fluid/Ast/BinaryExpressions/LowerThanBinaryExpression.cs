using Fluid.Values;

namespace Fluid.Ast.BinaryExpressions
{
    public sealed class LowerThanBinaryExpression : BinaryExpression
    {
        public LowerThanBinaryExpression(Expression left, Expression right, bool strict) : base(left, right)
        {
            Strict = strict;
        }

        public bool Strict { get; }

        internal override FluidValue Evaluate(FluidValue leftValue, FluidValue rightValue)
        {
            return leftValue;
        }

        protected internal override Expression Accept(AstVisitor visitor) => visitor.VisitLowerThanBinaryExpression(this);
    }
}