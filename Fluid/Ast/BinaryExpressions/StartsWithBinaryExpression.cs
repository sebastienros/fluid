using Fluid.Values;

namespace Fluid.Ast.BinaryExpressions
{
    public sealed class StartsWithBinaryExpression : BinaryExpression
    {
        public StartsWithBinaryExpression(Expression left, Expression right) : base(left, right)
        {
        }

        protected internal override Expression Accept(AstVisitor visitor) => visitor.VisitStartsWithBinaryExpression(this);

        public override async ValueTask<FluidValue> EvaluateAsync(TemplateContext context)
        {
            var leftValue = await Left.EvaluateAsync(context);
            var rightValue = await Right.EvaluateAsync(context);

            return await StartsWithAsync(leftValue, rightValue, context)
                        ? BooleanValue.True
                        : BooleanValue.False;
        }

        public static async ValueTask<bool> StartsWithAsync(FluidValue leftValue, FluidValue rightValue, TemplateContext context)
        {
            if (leftValue is ArrayValue)
            {
                var first = await leftValue.GetValueAsync("first", context);
                return first.Equals(rightValue);
            }
            else
            {
                return leftValue.ToStringValue().StartsWith(rightValue.ToStringValue());
            }
        }

        protected internal override Expression Accept(AstVisitor visitor) => visitor.VisitStartsWithBinaryExpression(this);
    }
}
