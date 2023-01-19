using Fluid.Values;

namespace Fluid.Ast.BinaryExpressions
{
    public sealed class EndsWithBinaryExpression : BinaryExpression
    {
        public EndsWithBinaryExpression(Expression left, Expression right) : base(left, right)
        {
        }

        protected internal override Expression Accept(AstVisitor visitor) => visitor.VisitEndsWithBinaryExpression(this);

        public override async ValueTask<FluidValue> EvaluateAsync(TemplateContext context)
        {
            var leftValue = await Left.EvaluateAsync(context);
            var rightValue = await Right.EvaluateAsync(context);

           return await EndsWithAsync(leftValue, rightValue, context)
                        ? BooleanValue.True
                        : BooleanValue.False;
        }

        public static async ValueTask<bool> EndsWithAsync(FluidValue leftValue, FluidValue rightValue, TemplateContext context)
        {
            if (leftValue is ArrayValue)
            {
                var first = await leftValue.GetValueAsync("last", context);
                return first.Equals(rightValue);
            }
            else
            {
                return leftValue.ToStringValue().EndsWith(rightValue.ToStringValue());
            }
        }
    }
}
