using Fluid.Values;

namespace Fluid.Ast.BinaryExpressions
{
    public sealed class EndsWithBinaryExpression : BinaryExpression
    {
        public EndsWithBinaryExpression(Expression left, Expression right) : base(left, right)
        {
        }

        public override async ValueTask<FluidValue> EvaluateAsync(TemplateContext context)
        {
            var leftValue = await Left.EvaluateAsync(context);
            var rightValue = await Right.EvaluateAsync(context);

            bool comparisonResult;
            if (leftValue is ArrayValue)
            {
                var first = await leftValue.GetValueAsync("last", context);
                comparisonResult = first.Equals(rightValue);
            }
            else
            {
                comparisonResult = leftValue.ToStringValue().EndsWith(rightValue.ToStringValue());
            }

            return new BinaryExpressionFluidValue(leftValue, comparisonResult);
        }

        protected internal override Expression Accept(AstVisitor visitor) => visitor.VisitEndsWithBinaryExpression(this);
    }
}
