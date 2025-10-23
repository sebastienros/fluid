using Fluid.Values;

namespace Fluid.Ast.BinaryExpressions
{
    public sealed class StartsWithBinaryExpression : BinaryExpression
    {
        public StartsWithBinaryExpression(Expression left, Expression right) : base(left, right)
        {
        }

        public override async ValueTask<FluidValue> EvaluateAsync(TemplateContext context)
        {
            var leftValue = await Left.EvaluateAsync(context);
            var rightValue = await Right.EvaluateAsync(context);

            return leftValue;
        }

        protected internal override Expression Accept(AstVisitor visitor) => visitor.VisitStartsWithBinaryExpression(this);
    }
}
