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

            // Shopify Liquid behavior: `contains` returns false if either operand is nil/false.
            // (see Liquid::Condition operators['contains'] guard: `if left && right && left.respond_to?(:include?)`).
            if (leftValue.IsNil() || (leftValue.Type == FluidValues.Boolean && !leftValue.ToBooleanValue())
                || rightValue.IsNil() || (rightValue.Type == FluidValues.Boolean && !rightValue.ToBooleanValue()))
            {
                return new BinaryExpressionFluidValue(leftValue, false);
            }

            var comparisonResult = await leftValue.ContainsAsync(rightValue, context);
            return new BinaryExpressionFluidValue(leftValue, comparisonResult);
        }

        protected internal override Expression Accept(AstVisitor visitor) => visitor.VisitContainsBinaryExpression(this);
    }
}