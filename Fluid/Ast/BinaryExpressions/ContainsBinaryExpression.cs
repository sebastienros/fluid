using Fluid.Values;

namespace Fluid.Ast.BinaryExpressions
{
    public sealed class ContainsBinaryExpression : BinaryExpression
    {
        public ContainsBinaryExpression(Expression left, Expression right) : base(left, right)
        {
        }

        public override async ValueTask<FluidValue> EvaluateAsync(TemplateContext context)
        {
            var leftValue = await Left.EvaluateAsync(context);
            var rightValue = await Right.EvaluateAsync(context);

            var comparisonResult = await leftValue.ContainsAsync(rightValue, context);
            return new BinaryExpressionFluidValue(leftValue, comparisonResult);
        }

        protected internal override Expression Accept(AstVisitor visitor) => visitor.VisitContainsBinaryExpression(this);
    }
}