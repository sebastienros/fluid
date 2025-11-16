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

            bool comparisonResult;
            if (leftValue is ArrayValue)
            {
                var first = await leftValue.GetValueAsync("first", context);
                comparisonResult = first.Equals(rightValue);
            }
            else
            {
                comparisonResult = leftValue.ToStringValue().StartsWith(rightValue.ToStringValue());
            }

            return new BinaryExpressionFluidValue(leftValue, comparisonResult);
        }

        protected internal override Expression Accept(AstVisitor visitor) => visitor.VisitStartsWithBinaryExpression(this);
    }
}
